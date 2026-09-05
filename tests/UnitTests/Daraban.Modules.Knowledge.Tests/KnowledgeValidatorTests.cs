using Daraban.Modules.Knowledge.Data.Entities;
using Daraban.Modules.Knowledge.Services.Dtos;
using Daraban.Modules.Knowledge.Services.Validators;
using Xunit;

namespace Daraban.Modules.Knowledge.Tests;

/// <summary>
/// FluentValidation rules for the Knowledge request DTOs. These run in the MVC pipeline before
/// a service method is ever reached, so they are the module's first line of defence against
/// oversized or malformed input.
/// </summary>
public class KnowledgeValidatorTests
{
    // ---- Category ---------------------------------------------------------------------------

    [Fact]
    public void CreateCategory_Requires_A_Name()
    {
        var result = new CreateKbCategoryRequestValidator()
            .Validate(new CreateKbCategoryRequest(null, "", null, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateKbCategoryRequest.Name));
    }

    [Fact]
    public void CreateCategory_Rejects_An_Over_Long_Name()
    {
        var result = new CreateKbCategoryRequestValidator()
            .Validate(new CreateKbCategoryRequest(null, new string('x', 201), null, null));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("Network Devices")]  // spaces
    [InlineData("network--devices")] // doubled hyphen
    [InlineData("-network")]         // leading hyphen
    [InlineData("network-")]         // trailing hyphen
    [InlineData("Réseau")]           // non-ASCII
    public void CreateCategory_Rejects_A_Malformed_Explicit_Slug(string slug)
    {
        var result = new CreateKbCategoryRequestValidator()
            .Validate(new CreateKbCategoryRequest(null, "Name", slug, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateKbCategoryRequest.Slug));
    }

    [Theory]
    [InlineData("network")]
    [InlineData("network-devices")]
    [InlineData("vpn2")]
    public void CreateCategory_Accepts_A_Wellformed_Slug(string slug)
    {
        var result = new CreateKbCategoryRequestValidator()
            .Validate(new CreateKbCategoryRequest(null, "Name", slug, null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateCategory_Allows_An_Omitted_Slug_Because_The_Service_Derives_One()
    {
        var result = new CreateKbCategoryRequestValidator()
            .Validate(new CreateKbCategoryRequest(null, "Network Devices", null, null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateCategory_Rejects_A_Negative_SortOrder()
    {
        var result = new CreateKbCategoryRequestValidator()
            .Validate(new CreateKbCategoryRequest(null, "Name", null, null, SortOrder: -1));

        Assert.False(result.IsValid);
    }

    // ---- Article ----------------------------------------------------------------------------

    [Fact]
    public void CreateArticle_Requires_Title_And_Content()
    {
        var result = new CreateKbArticleRequestValidator()
            .Validate(new CreateKbArticleRequest("", "", null, null, false, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateKbArticleRequest.Title));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateKbArticleRequest.Content));
    }

    [Fact]
    public void CreateArticle_Caps_Content_At_100k_Characters()
    {
        var validator = new CreateKbArticleRequestValidator();

        // The column itself is unbounded text; this ceiling stops a runaway paste from becoming a
        // multi-megabyte row that the tsvector generator then has to chew through on every write.
        Assert.True(validator.Validate(
            new CreateKbArticleRequest("T", new string('x', 100_000), null, null, false, null, null)).IsValid);

        Assert.False(validator.Validate(
            new CreateKbArticleRequest("T", new string('x', 100_001), null, null, false, null, null)).IsValid);
    }

    [Fact]
    public void CreateArticle_Rejects_A_Target_With_An_Empty_Guid()
    {
        var result = new CreateKbArticleRequestValidator().Validate(
            new CreateKbArticleRequest("T", "C", null, null, false, null,
                new[] { new KbArticleTargetInput(KbTargetType.Group, Guid.Empty) }));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void CreateArticle_Rejects_An_Undefined_Target_Type()
    {
        var result = new CreateKbArticleRequestValidator().Validate(
            new CreateKbArticleRequest("T", "C", null, null, false, null,
                new[] { new KbArticleTargetInput((KbTargetType)99, Guid.CreateVersion7()) }));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void CreateArticle_Accepts_A_Minimal_Valid_Request()
    {
        var result = new CreateKbArticleRequestValidator()
            .Validate(new CreateKbArticleRequest("VPN reset", "Steps...", null, null, false, null, null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateArticle_Applies_The_Same_Rules_As_Create()
    {
        var result = new UpdateKbArticleRequestValidator()
            .Validate(new UpdateKbArticleRequest("", "", null, null, false, new string('t', 501), null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateKbArticleRequest.Title));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateKbArticleRequest.Content));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateKbArticleRequest.Tags));
    }

    [Fact]
    public void ChangeStatus_Rejects_An_Undefined_Status()
    {
        var validator = new ChangeKbArticleStatusRequestValidator();

        Assert.False(validator.Validate(new ChangeKbArticleStatusRequest((KbArticleStatus)42)).IsValid);
        Assert.True(validator.Validate(new ChangeKbArticleStatusRequest(KbArticleStatus.Published)).IsValid);
    }

    // ---- Feedback + ticket link -------------------------------------------------------------

    [Fact]
    public void SubmitFeedback_Caps_The_Comment_Length()
    {
        var validator = new SubmitKbFeedbackRequestValidator();

        Assert.True(validator.Validate(new SubmitKbFeedbackRequest(true, new string('c', 2000))).IsValid);
        Assert.False(validator.Validate(new SubmitKbFeedbackRequest(true, new string('c', 2001))).IsValid);
    }

    [Fact]
    public void SubmitFeedback_Allows_A_Verdict_With_No_Comment()
    {
        var result = new SubmitKbFeedbackRequestValidator().Validate(new SubmitKbFeedbackRequest(false, null));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void LinkArticleToTicket_Requires_An_ArticleId()
    {
        var validator = new LinkKbArticleToTicketRequestValidator();

        Assert.False(validator.Validate(new LinkKbArticleToTicketRequest(Guid.Empty, true, null)).IsValid);
        Assert.True(validator.Validate(new LinkKbArticleToTicketRequest(Guid.CreateVersion7(), true, null)).IsValid);
    }

    [Fact]
    public void LinkArticleToTicket_Caps_The_Note_Length()
    {
        var result = new LinkKbArticleToTicketRequestValidator().Validate(
            new LinkKbArticleToTicketRequest(Guid.CreateVersion7(), true, new string('n', 1001)));

        Assert.False(result.IsValid);
    }
}
