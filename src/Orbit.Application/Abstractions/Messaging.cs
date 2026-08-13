using MediatR;

namespace Orbit.Application.Abstractions;

public interface ICommand<out TResponse> : IRequest<TResponse>;

public interface IQuery<out TResponse> : IRequest<TResponse>;
