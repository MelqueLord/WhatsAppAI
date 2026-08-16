using WhatsAppAI.Domain;
using WhatsAppAI.Domain.Knowledge;
using Xunit;

namespace WhatsAppAI.UnitTests.Knowledge;

public class KnowledgeItemTests
{
    [Fact]
    public void Create_SetsIsActiveTrue()
    {
        var item = KnowledgeItem.Create(Guid.NewGuid(), "Title", "Content");

        Assert.True(item.IsActive);
        Assert.Equal(1u, item.Version);
    }

    [Fact]
    public void Create_TrimsTitleAndContent()
    {
        var item = KnowledgeItem.Create(Guid.NewGuid(), "  Title  ", "  Content  ");

        Assert.Equal("Title", item.Title);
        Assert.Equal("Content", item.Content);
    }

    [Fact]
    public void Create_SetsPriority()
    {
        var item = KnowledgeItem.Create(Guid.NewGuid(), "Title", "Content", 5);

        Assert.Equal(5, item.Priority);
    }

    [Fact]
    public void Update_ChangesProperties()
    {
        var item = KnowledgeItem.Create(Guid.NewGuid(), "Title", "Content");
        item.Update("New Title", "New Content", 10, 1);

        Assert.Equal("New Title", item.Title);
        Assert.Equal("New Content", item.Content);
        Assert.Equal(10, item.Priority);
    }

    [Fact]
    public void Update_IncrementsVersion()
    {
        var item = KnowledgeItem.Create(Guid.NewGuid(), "Title", "Content");
        item.Update("New", "New", 0, 1);

        Assert.Equal(2u, item.Version);
    }

    [Fact]
    public void Update_ThrowsOnVersionConflict()
    {
        var item = KnowledgeItem.Create(Guid.NewGuid(), "Title", "Content");

        Assert.Throws<ConcurrencyException>(() =>
            item.Update("New", "New", 0, 999));
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var item = KnowledgeItem.Create(Guid.NewGuid(), "Title", "Content");
        item.Deactivate(1);

        Assert.False(item.IsActive);
        Assert.NotNull(item.DeactivatedAt);
    }

    [Fact]
    public void Deactivate_IncrementsVersion()
    {
        var item = KnowledgeItem.Create(Guid.NewGuid(), "Title", "Content");
        item.Deactivate(1);

        Assert.Equal(2u, item.Version);
    }

    [Fact]
    public void Deactivate_ThrowsWhenAlreadyDeactivated()
    {
        var item = KnowledgeItem.Create(Guid.NewGuid(), "Title", "Content");
        item.Deactivate(1);

        Assert.Throws<InvalidOperationException>(() =>
            item.Deactivate(2));
    }

    [Fact]
    public void Deactivate_ThrowsOnVersionConflict()
    {
        var item = KnowledgeItem.Create(Guid.NewGuid(), "Title", "Content");

        Assert.Throws<ConcurrencyException>(() =>
            item.Deactivate(999));
    }

    [Fact]
    public void Reactivate_SetsIsActiveTrue()
    {
        var item = KnowledgeItem.Create(Guid.NewGuid(), "Title", "Content");
        item.Deactivate(1);
        item.Reactivate(2);

        Assert.True(item.IsActive);
        Assert.NotNull(item.ReactivatedAt);
    }

    [Fact]
    public void Reactivate_ThrowsWhenAlreadyActive()
    {
        var item = KnowledgeItem.Create(Guid.NewGuid(), "Title", "Content");

        Assert.Throws<InvalidOperationException>(() =>
            item.Reactivate(1));
    }

    [Fact]
    public void Reactivate_ThrowsOnVersionConflict()
    {
        var item = KnowledgeItem.Create(Guid.NewGuid(), "Title", "Content");
        item.Deactivate(1);

        Assert.Throws<ConcurrencyException>(() =>
            item.Reactivate(999));
    }
}
