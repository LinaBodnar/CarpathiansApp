using Carpathians.Reviews.Application.Common.Messaging;

namespace Carpathians.Reviews.Application.Reviews.Commands.DeleteReview;

public record DeleteReviewCommand(string Id, int? ExpectedVersion = null) : ICommand;
