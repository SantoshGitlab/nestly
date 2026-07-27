using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Services.Identity;
using Nestly.Domain.ValueObjects;

namespace ConsumerApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ForgotPasswordController : ControllerBase
    {
        private readonly IIdentityService _identityService;

        public ForgotPasswordController(IIdentityService identityService)
        {
            _identityService = identityService;
        }

        [HttpPost("send-verification-code")]
        public async Task<IActionResult> SendVerificationCode([FromBody] ForgotPasswordRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _identityService.SendForgotPasswordVerificationCodeAsync(request.Email, request.Mobile);
            if (result.IsFailure)
                return StatusCode(result.Error.Code, result.Error.Message);

            return Ok();
        }

        [HttpPost("verify-code")]
        public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _identityService.VerifyForgotPasswordCodeAsync(request.Email, request.Mobile, request.Code);
            if (result.IsFailure)
                return StatusCode(result.Error.Code, result.Error.Message);

            return Ok();
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _identityService.ResetForgotPasswordAsync(request.Email, request.Mobile, request.NewPassword);
            if (result.IsFailure)
                return StatusCode(result.Error.Code, result.Error.Message);

            return Ok();
        }
    }

    public class ForgotPasswordRequest
    {
        [Required]
        public string Email { get; set; }
        [Required]
        public string Mobile { get; set; }
    }

    public class VerifyCodeRequest
    {
        [Required]
        public string Email { get; set; }
        [Required]
        public string Mobile { get; set; }
        [Required]
        public string Code { get; set; }
    }

    public class ResetPasswordRequest
    {
        [Required]
        public string Email { get; set; }
        [Required]
        public string Mobile { get; set; }
        [Required]
        public string NewPassword { get; set; }
    }
}
