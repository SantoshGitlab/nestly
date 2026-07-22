using MediatR;
using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Abstractions;

/// <summary>Command without a return value.</summary>
public interface ICommand : IRequest<Result>;

/// <summary>Command returning <typeparamref name="TResponse"/>.</summary>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>;

/// <summary>Query returning <typeparamref name="TResponse"/>.</summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>;

public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand;

public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
