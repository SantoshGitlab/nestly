#!/usr/bin/env python3
"""
Generates the "PART 3 — FULL ENDPOINT REFERENCE" section of docs/API.md by
cross-referencing each API's real OpenAPI document (fetched by
generate-openapi.sh) against its controller source for the one thing
Swashbuckle can't supply here: the actual /// <summary> doc comments (this
solution has no GenerateDocumentationFile/IncludeXmlComments wired, so the
generated OpenAPI JSON's "summary" fields are empty) and the [Authorize]
attributes.

Not meant to be run directly — see scripts/generate-openapi.sh, which builds
the APIs, fetches their swagger.json, and invokes this with the right paths.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

HTTP_VERBS = ("HttpGet", "HttpPost", "HttpPut", "HttpPatch", "HttpDelete")

# OpenAPI operations that don't come from a Controllers/*Controller.cs action
# (minimal `app.Map*` endpoints mapped directly in Program.cs) can't be
# resolved by the source-scan below, which only walks controller files.
# There should be very few of these; each one is hand-verified against
# Program.cs and listed here so the table doesn't guess at its auth model.
KNOWN_MINIMAL_ENDPOINTS: dict[tuple[str, str], tuple[str, str]] = {
    ("post", "/api/v1/auth/dev/login-as-provider"): (
        "Dev-only QA auth bypass (Program.cs, provider-api) — issues a real "
        "provider session for a given mobile number without OTP/password. "
        "Gated by a shared-secret header, not a JWT.",
        "Header `X-Dev-Auth-Key` must match `DevAuth:Key` config; no key "
        "configured (as in Production) makes this endpoint unusable.",
    ),
}

ROUTE_CONSTRAINT_RE = re.compile(r"\{(\w+):[^}]+\}")


def strip_route_constraints(route: str) -> str:
    return ROUTE_CONSTRAINT_RE.sub(r"{\1}", route)


@dataclass
class Action:
    http_method: str  # get/post/put/patch/delete
    path: str  # normalized, leading slash, matches OpenAPI path key
    summary: str | None
    auth: str  # human-readable auth requirement


@dataclass
class ControllerInfo:
    class_name: str
    doc_summary: str | None
    actions: list[Action] = field(default_factory=list)


# Matches an attribute block (one or more `[...]` lines, possibly multi-line)
# immediately followed by a method signature line containing IActionResult /
# ActionResult<...> / Task<IActionResult> etc, itself possibly preceded by a
# /// <summary> doc-comment block.
METHOD_BLOCK_RE = re.compile(
    r"(?P<doc>(?:^[ \t]*///.*\n)*)"
    r"(?P<attrs>(?:^[ \t]*\[[^\]]*(?:\][ \t]*\n(?:^[ \t]*\[[^\]]*)?)*\][ \t]*\n)+)"
    r"^[ \t]*public\s+(?:async\s+)?(?:Task<)?(?:ActionResult(?:<[^>]*>)?|IActionResult)>?\s+(?P<name>\w+)\s*\(",
    re.MULTILINE,
)

# A single attribute line/group like `[HttpGet("foo/{id}")]` or
# `[Authorize(Policy = X)]`, possibly spanning multiple physical lines.
ATTR_RE = re.compile(r"\[(?P<body>[^\[\]]*)\]")


def extract_summary(doc_block: str) -> str | None:
    """Pulls the text of /// <summary>...</summary> out of a doc-comment block."""
    text = "\n".join(
        line.strip().removeprefix("///").strip()
        for line in doc_block.splitlines()
        if line.strip().startswith("///")
    )
    m = re.search(r"<summary>(.*?)</summary>", text, re.DOTALL)
    if not m:
        return None
    summary = re.sub(r"\s+", " ", m.group(1)).strip()
    # Turn xmldoc cross-reference tags into plain, readable text instead of
    # dropping them silently (a bare <see cref="X"/> renders as nothing and
    # leaves a dangling "as , which" in the sentence).
    summary = re.sub(r'<see cref="[^"]*?([.:])?(\w+)"\s*/>', r"`\2`", summary)
    summary = re.sub(r'<paramref name="([^"]*)"\s*/>', r"`\1`", summary)
    summary = re.sub(r"<c>(.*?)</c>", r"`\1`", summary)
    summary = re.sub(r"\s+([,.])", r"\1", summary)  # tidy up spacing left by tag removal
    return summary.strip() or None


def parse_class_header(text: str) -> tuple[str, str | None, str, bool]:
    """Returns (class_name, controller_route, class_attrs_text, controller_level_authorize_present)."""
    m = re.search(r"^[ \t]*public\s+class\s+(\w+)\s*:\s*ControllerBase", text, re.MULTILINE)
    if not m:
        raise ValueError("no ControllerBase class found")
    class_name = m.group(1)
    # Attributes block directly above the class declaration.
    pre = text[: m.start()]
    attr_lines = []
    for line in reversed(pre.splitlines()):
        stripped = line.strip()
        if stripped.startswith("[") or (attr_lines and not stripped.startswith("///") and stripped != "" and not stripped.startswith("public class")):
            if stripped.startswith("["):
                attr_lines.append(stripped)
                continue
        if stripped == "":
            if attr_lines:
                continue
            else:
                continue
        if stripped.startswith("///"):
            break
        if not stripped.startswith("["):
            break
    attrs_text = "\n".join(reversed(attr_lines))
    route_m = re.search(r'\[Route\("([^"]+)"\)\]', attrs_text)
    controller_route = route_m.group(1) if route_m else ""
    has_authorize = "[Authorize" in attrs_text and "AllowAnonymous" not in attrs_text
    return class_name, controller_route, attrs_text, has_authorize


def build_admin_module_map(domain_dir: Path) -> dict[str, str]:
    """Parses AdminModules.cs's `public const string Name = "value";` pairs."""
    f = domain_dir / "AdminModules.cs"
    if not f.exists():
        return {}
    text = f.read_text()
    return dict(re.findall(r'public const string (\w+)\s*=\s*"([^"]+)";', text))


def local_policy_consts(text: str, module_map: dict[str, str]) -> dict[str, str]:
    """Resolves this controller's `private const string XPolicy = AdminModules.Y + ".z";`
    (or plain string literal) declarations into their actual permission strings."""
    consts: dict[str, str] = {}
    for name, expr in re.findall(r'(?:private|public)\s+const\s+string\s+(\w+)\s*=\s*([^;]+);', text):
        expr = expr.strip()
        m = re.match(r'AdminModules\.(\w+)\s*\+\s*"([^"]+)"', expr)
        if m:
            module_val = module_map.get(m.group(1), m.group(1))
            consts[name] = f"{module_val}{m.group(2)}"
            continue
        m2 = re.match(r'"([^"]+)"$', expr)
        if m2:
            consts[name] = m2.group(1)
    return consts


def classify_auth(
    attrs_text: str,
    controller_authorize_attrs: str,
    controller_has_authorize: bool,
    policy_consts: dict[str, str] | None = None,
) -> str:
    """Combines controller-level and method-level [Authorize]/[AllowAnonymous] into a human label."""
    if "[AllowAnonymous]" in attrs_text:
        return "Public"

    method_has_authorize = "[Authorize" in attrs_text
    if not method_has_authorize and not controller_has_authorize:
        return "Public"

    # A method-level [Authorize(Policy = X)] usually omits AuthenticationSchemes
    # (it inherits the controller's scheme) — so scheme detection must look at
    # both levels, while policy detection prefers the method's own value and
    # only falls back to the controller's when the method didn't set one.
    scheme_source = attrs_text + " " + (controller_authorize_attrs if controller_has_authorize else "")
    if "AdminJwtBearerScheme" in scheme_source:
        scheme = "Admin JWT"
    elif "ProviderJwtBearerScheme" in scheme_source:
        scheme = "Provider JWT"
    else:
        scheme = "Customer JWT"

    policy_source = attrs_text if re.search(r"Policy\s*=", attrs_text) else controller_authorize_attrs
    policy_m = re.search(r"Policy\s*=\s*([A-Za-z0-9_.\"+ ]+)", policy_source)
    if policy_m:
        policy_expr = policy_m.group(1).strip().rstrip(",")
        if policy_consts and policy_expr in policy_consts:
            resolved = policy_consts[policy_expr]
        else:
            # Turn `AdminModules.NestlyCoins + ".read"` into a readable
            # permission string — best effort, not a full C# eval.
            resolved = re.sub(r'\s*\+\s*"', "", policy_expr).rstrip('"').replace('"', "")
        return f"{scheme} + permission `{resolved}`"

    roles_m = re.search(r'Roles\s*=\s*"([^"]+)"', scheme_source)
    if roles_m:
        return f"{scheme} (role: {roles_m.group(1)})"

    return scheme


def parse_controller(path: Path, module_map: dict[str, str] | None = None) -> ControllerInfo:
    text = path.read_text()
    class_name, controller_route, class_attrs, controller_has_authorize = parse_class_header(text)
    policy_consts = local_policy_consts(text, module_map or {})

    class_doc_m = re.search(
        r"((?:^[ \t]*///.*\n)+)^[ \t]*\[ApiController\]", text, re.MULTILINE
    )
    class_doc = extract_summary(class_doc_m.group(1)) if class_doc_m else None

    controller_route = strip_route_constraints(controller_route)

    info = ControllerInfo(class_name=class_name, doc_summary=class_doc)

    for m in METHOD_BLOCK_RE.finditer(text):
        attrs_text = m.group("attrs")
        doc_text = m.group("doc")
        verb = None
        route_suffix = ""
        for attr_m in ATTR_RE.finditer(attrs_text):
            body = attr_m.group("body").strip()
            for v in HTTP_VERBS:
                if body == v or body.startswith(v + "("):
                    verb = v[4:].lower()
                    arg_m = re.match(rf'{v}\("([^"]*)"\)', body)
                    route_suffix = arg_m.group(1) if arg_m else ""
                    break
            if verb:
                break
        if not verb:
            continue  # not an HTTP action (helper method, constructor, etc.)

        route_suffix = strip_route_constraints(route_suffix)
        if route_suffix:
            full_path = f"{controller_route}/{route_suffix}" if controller_route else route_suffix
        else:
            full_path = controller_route
        full_path = "/" + full_path.strip("/")
        full_path = full_path.replace("v{version:apiVersion}", "v{version}")

        summary = extract_summary(doc_text)
        auth = classify_auth(attrs_text, class_attrs, controller_has_authorize, policy_consts)

        info.actions.append(Action(http_method=verb, path=full_path, summary=summary, auth=auth))

    return info


def parse_controllers(src_dir: Path, module_map: dict[str, str] | None = None) -> dict[str, ControllerInfo]:
    result: dict[str, ControllerInfo] = {}
    for f in sorted(src_dir.glob("*Controller.cs")):
        info = parse_controller(f, module_map)
        result[info.class_name] = info
    return result


def resolve_schema_name(schema: dict) -> str:
    if not schema:
        return ""
    if "$ref" in schema:
        return schema["$ref"].rsplit("/", 1)[-1]
    if schema.get("type") == "array":
        return f"{resolve_schema_name(schema.get('items', {}))}[]"
    t = schema.get("type")
    fmt = schema.get("format")
    if t:
        return f"{t}{'/' + fmt if fmt else ''}"
    return "object"


def describe_request_body(op: dict) -> str:
    rb = op.get("requestBody")
    if not rb:
        return "—"
    content = rb.get("content", {})
    for ct in ("application/json", "text/json"):
        if ct in content:
            return resolve_schema_name(content[ct].get("schema", {}))
    if content:
        first = next(iter(content.values()))
        return resolve_schema_name(first.get("schema", {}))
    return "—"


def describe_success_response(op: dict) -> str:
    responses = op.get("responses", {})
    for code in ("200", "201", "202"):
        if code in responses:
            content = responses[code].get("content", {})
            for ct in ("application/json", "text/json"):
                if ct in content:
                    return f"{code} → {resolve_schema_name(content[ct]['schema'])}"
            return f"{code} {responses[code].get('description', '')}".strip()
    if "204" in responses:
        return "204 No Content"
    # fall back to whatever is documented, lowest code first
    for code in sorted(responses, key=lambda c: (not c[0].isdigit(), c)):
        return f"{code} {responses[code].get('description', '')}".strip()
    return "—"


def md_escape(s: str) -> str:
    return s.replace("|", "\\|") if s else s


def build_section(api_title: str, openapi: dict, controllers: dict[str, ControllerInfo]) -> tuple[str, int, int]:
    # Build a lookup of (method, path) -> Action across all controllers.
    action_lookup: dict[tuple[str, str], tuple[str, Action]] = {}
    for cname, cinfo in controllers.items():
        for action in cinfo.actions:
            action_lookup[(action.http_method, action.path)] = (cname, action)

    # Group OpenAPI operations by tag (Swashbuckle tags = controller name
    # sans "Controller", or the default minimal-API tag for non-controller
    # endpoints such as the dev-only test-auth route).
    by_tag: dict[str, list[tuple[str, str, dict]]] = {}
    op_count = 0
    for path, methods in openapi.get("paths", {}).items():
        for method, op in methods.items():
            if method not in ("get", "post", "put", "patch", "delete"):
                continue
            op_count += 1
            tags = op.get("tags") or ["Untagged"]
            by_tag.setdefault(tags[0], []).append((method, path, op))

    lines = [f"## {api_title}", ""]
    unmatched = 0
    for tag in sorted(by_tag):
        controller_name = f"{tag}Controller"
        cinfo = controllers.get(controller_name)
        heading = f"### {tag}"
        lines.append(heading)
        if cinfo and cinfo.doc_summary:
            lines.append("")
            lines.append(cinfo.doc_summary)
        lines.append("")
        lines.append("| Method | Path | Summary | Auth | Request | Success Response |")
        lines.append("|---|---|---|---|---|---|")
        for method, path, op in sorted(by_tag[tag], key=lambda x: (x[1], x[0])):
            match = action_lookup.get((method, path))
            if match:
                _, action = match
                summary = action.summary or "_(no doc comment)_"
                auth = action.auth
            elif (method, path) in KNOWN_MINIMAL_ENDPOINTS:
                summary, auth = KNOWN_MINIMAL_ENDPOINTS[(method, path)]
            else:
                unmatched += 1
                summary = "_(not matched to a controller source action — likely a minimal-API endpoint mapped in Program.cs; verify manually)_"
                auth = "UNVERIFIED — check Program.cs"
            req = describe_request_body(op)
            resp = describe_success_response(op)
            lines.append(
                f"| {method.upper()} | `{path}` | {md_escape(summary)} | {auth} | {md_escape(req)} | {md_escape(resp)} |"
            )
        lines.append("")

    return "\n".join(lines), op_count, unmatched


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--consumer-json", type=Path, required=True)
    p.add_argument("--consumer-src", type=Path, required=True)
    p.add_argument("--admin-json", type=Path, required=True)
    p.add_argument("--admin-src", type=Path, required=True)
    p.add_argument("--provider-json", type=Path, required=True)
    p.add_argument("--provider-src", type=Path, required=True)
    p.add_argument("--api-md", type=Path, required=True)
    p.add_argument("--commit", default=None, help="commit hash to stamp the section with")
    p.add_argument("--date", default=None, help="date to stamp the section with (YYYY-MM-DD)")
    args = p.parse_args()

    apis = [
        ("consumer-api", "CONSUMER-API (customer-facing)", args.consumer_json, args.consumer_src),
        ("admin-api", "ADMIN-API (internal ops console)", args.admin_json, args.admin_src),
        ("provider-api", "PROVIDER-API (provider mobile/web)", args.provider_json, args.provider_src),
    ]

    domain_dir = args.api_md.parent.parent / "backend" / "shared" / "Domain"
    module_map = build_admin_module_map(domain_dir)

    total_controllers = 0
    total_ops = 0
    total_unmatched = 0
    sections = []
    for key, title, json_path, src_dir in apis:
        openapi = json.loads(json_path.read_text())
        controllers = parse_controllers(src_dir, module_map if key == "admin-api" else None)
        total_controllers += len(controllers)
        section, op_count, unmatched = build_section(title, openapi, controllers)
        total_ops += op_count
        total_unmatched += unmatched
        sections.append(section)
        print(f"{key}: {len(controllers)} controllers, {op_count} operations, {unmatched} unmatched to source", file=sys.stderr)

    import datetime
    import subprocess

    commit = args.commit
    if not commit:
        try:
            commit = subprocess.check_output(
                ["git", "rev-parse", "--short", "HEAD"], cwd=args.api_md.parent, text=True
            ).strip()
        except Exception:
            commit = "unknown"
    date = args.date or datetime.date.today().isoformat()

    header = f"""# PART 3 — FULL ENDPOINT REFERENCE (GENERATED)

<!-- BEGIN GENERATED ENDPOINT REFERENCE -->

Generated by `scripts/generate-openapi.sh` (which drives
`scripts/generate_endpoint_reference.py`) against the real OpenAPI documents
Swashbuckle produces for each API (`AddSwaggerGen`/`UseSwagger`), cross-referenced
with each controller action's `/// <summary>` doc comment and
`[Authorize]`/`[AllowAnonymous]` attributes — this solution has no
`IncludeXmlComments` wired, so the raw OpenAPI JSON's own summaries are
empty and the real one-line descriptions have to come from source.

**Generated against commit `{commit}` on {date}**: {total_controllers} controllers,
{total_ops} operations across the three APIs. Routes, request/response shapes
and status codes are reflection-derived from the code and cannot drift from
it *as of that commit*; controller doc comments can still be edited without
re-running this script, and new controllers won't appear until it's re-run.
Treat this table as a snapshot, not a live feed — regenerate it
(`scripts/generate-openapi.sh`) whenever the controller surface changes
materially, the same way this repo's other generated/audited docs
(docs/TRACKING.md, docs/ORIENTATION.md) flag their own staleness rather than
pretending to be permanently current.

"Success Response" shows only the 2xx/204 case. Every non-2xx response not
shown here follows the `ProblemDetails` shape documented earlier in this
file (see ERROR RESPONSE / PAYLOAD SCHEMAS) unless the table says otherwise —
that convention is enforced by `GlobalExceptionHandlingMiddleware` for
unhandled failures and by each controller's explicit
`[ProducesResponseType]` list for the rest, so it is not repeated per row.

"""

    body = "\n".join(sections)
    full_section = header + body + "\n<!-- END GENERATED ENDPOINT REFERENCE -->\n"

    md = args.api_md.read_text()
    begin_marker = "# PART 3 — FULL ENDPOINT REFERENCE (GENERATED)"
    if begin_marker in md:
        pre = md[: md.index(begin_marker)]
        md = pre.rstrip() + "\n\n" + full_section
    else:
        md = md.rstrip() + "\n\n" + full_section
    args.api_md.write_text(md)

    print(f"TOTAL: {total_controllers} controllers, {total_ops} operations, {total_unmatched} unmatched", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
