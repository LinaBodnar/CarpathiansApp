using Carpathians.Reviews.Application.Common.Messaging;
using Carpathians.Reviews.Application.Common.Models;

namespace Carpathians.Reviews.Application.Reviews.Commands.AddReviewComment;

/// <summary>Додавання коментаря у вбудований масив reviews.comments.</summary>
public record AddReviewCommentCommand(
    string ReviewId,
    string AuthorId,
    string AuthorName,
    string Text) : ICommand<ReviewDetailsDto>;
