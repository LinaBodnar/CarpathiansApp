using Carpathians.Reviews.Application.Common.Messaging;
using Carpathians.Reviews.Application.Discussions.Commands.CreateDiscussion;
using Carpathians.Reviews.Application.Discussions.Commands.PostDiscussionMessage;
using Carpathians.Reviews.Application.Reviews.Commands.AddReviewComment;
using Carpathians.Reviews.Application.Reviews.Commands.CreateReview;
using Carpathians.Reviews.Application.Reviews.Commands.DeleteReview;
using Carpathians.Reviews.Application.Reviews.Commands.UpdateReview;
using MediatR;

namespace Carpathians.Reviews.WebApi.Metrics;

/// <summary>
/// MediatR behavior з бізнес-метриками — найзовнішній у конвеєрі (до логування й валідації), тож бачить
/// кожну команду й запит CQRS з REST і gRPC, включно з невдалою валідацією.
/// Тривалість записується для всіх виконань; бізнес-лічильник — одразу після успішної команди.
/// </summary>
public sealed class MetricsBehavior<TRequest, TResponse>(ReviewsMetrics metrics)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    /// <summary>Назва операції з типу: CreateReviewCommand -> create_review (фіксований перелік типів).</summary>
    private static readonly string Operation = ToOperationName(typeof(TRequest).Name);

    private static readonly string Kind = IsCommand(typeof(TRequest)) ? "command" : "query";

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var response = await metrics.MeasureAsync(Operation, Kind, () => next());
        CountBusinessEvent(request);
        return response;
    }

    private void CountBusinessEvent(TRequest request)
    {
        switch (request)
        {
            case CreateReviewCommand create:
                metrics.ReviewCreated(create.Rating);
                break;
            case UpdateReviewCommand:
                metrics.ReviewUpdated();
                break;
            case DeleteReviewCommand:
                metrics.ReviewDeleted();
                break;
            case AddReviewCommentCommand:
                metrics.CommentAdded();
                break;
            case CreateDiscussionCommand:
                metrics.DiscussionCreated();
                break;
            case PostDiscussionMessageCommand:
                metrics.DiscussionMessagePosted();
                break;
        }
    }

    private static bool IsCommand(Type type)
        => typeof(ICommand).IsAssignableFrom(type)
           || type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));

    private static string ToOperationName(string typeName)
    {
        var name = typeName.EndsWith("Command", StringComparison.Ordinal) ? typeName[..^"Command".Length]
            : typeName.EndsWith("Query", StringComparison.Ordinal) ? typeName[..^"Query".Length]
            : typeName;

        var builder = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0)
                builder.Append('_');
            builder.Append(char.ToLowerInvariant(name[i]));
        }
        return builder.ToString();
    }
}
