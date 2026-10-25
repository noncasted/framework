using FluentAssertions;
using Internal;
using Xunit;

namespace Framework.Common.Tests;

/// <summary>
/// Entries of the viewable collections: their lifetimes and events.
/// </summary>
public class ReactiveCollectionsTests
{
    [Fact]
    public void List_Remove_EndsEntryLifetime_AfterEntryLeftTheList()
    {
        var list = new ViewableList<string>();
        var lifetime = list.Add("a");
        var countAtEnd = -1;
        lifetime.Listen(() => countAtEnd = list.Count);

        list.Remove("a");

        lifetime.IsTerminated.Should().BeTrue();
        countAtEnd.Should().Be(0);
    }

    [Fact]
    public void List_RemovedEntryAddedAgain_GetsNewLifetime()
    {
        var list = new ViewableList<string>();
        var first = list.Add("a");
        list.Remove("a");

        var second = list.Add("a");

        first.IsTerminated.Should().BeTrue();
        second.IsTerminated.Should().BeFalse();
        list.GetLifetime("a").Should().BeSameAs(second);
    }

    [Fact]
    public void List_RemoveUnknown_Throws()
    {
        var list = new ViewableList<string>();

        var act = () => list.Remove("missing");

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void List_Insert_GivesEntryLifetime_AtIndex()
    {
        var list = new ViewableList<string>();
        list.Add("a");
        list.Add("c");

        var lifetime = list.Insert(1, "b");

        list.Should().Equal("a", "b", "c");
        list.GetLifetime("b").Should().BeSameAs(lifetime);
    }

    [Fact]
    public void List_Clear_EmptiesAndEndsEveryEntry()
    {
        var list = new ViewableList<string>();
        var a = list.Add("a");
        var b = list.Add("b");

        list.Clear();

        list.Should().BeEmpty();
        a.IsTerminated.Should().BeTrue();
        b.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void List_AfterDispose_AddReturnsTerminatedLifetime_ListenRunsAtOnce()
    {
        var list = new ViewableList<string>();
        list.Dispose();
        var called = false;

        var lifetime = list.Add("a");
        lifetime.Listen(() => called = true);

        lifetime.IsTerminated.Should().BeTrue();
        called.Should().BeTrue();
        list.Should().BeEmpty();
    }

    [Fact]
    public void List_View_SeesExistingAndAdded_StopsWithSubscriber()
    {
        var list = new ViewableList<string>();
        list.Add("a");
        var subscriber = new Lifetime();
        var seen = new List<string>();

        list.View(subscriber, value => seen.Add(value));
        list.Add("b");
        subscriber.Terminate();
        list.Add("c");

        seen.Should().Equal("a", "b");
    }

    [Fact]
    public void Dictionary_RemovedKeyAddedAgain_GetsNewLifetime()
    {
        var dictionary = new ViewableDictionary<int, string>();
        var first = dictionary.Add(1, "a");
        dictionary.Remove(1);

        var second = dictionary.Add(1, "b");

        first.IsTerminated.Should().BeTrue();
        second.IsTerminated.Should().BeFalse();
        dictionary.GetLifetime(1).Should().BeSameAs(second);
    }

    [Fact]
    public void Dictionary_RemoveUnknown_Throws()
    {
        var dictionary = new ViewableDictionary<int, string>();

        var act = () => dictionary.Remove(1);

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Dictionary_IndexerSet_ReplacesValue_RenewsEntryLifetime()
    {
        var dictionary = new ViewableDictionary<int, string>();
        var first = dictionary.Add(1, "a");
        var seen = new List<string>();
        dictionary.Advise(new Lifetime(), (_, _, value) => seen.Add(value));

        dictionary[1] = "b";
        dictionary[2] = "c";

        first.IsTerminated.Should().BeTrue();
        dictionary[1].Should().Be("b");
        dictionary.GetLifetime(1).IsTerminated.Should().BeFalse();
        seen.Should().Equal("b", "c");
    }

    // Dispose ends the entry lifetimes, as Remove does for one entry.
    [Fact]
    public void Dictionary_Dispose_EndsEveryEntryLifetime()
    {
        var dictionary = new ViewableDictionary<int, string>();
        var a = dictionary.Add(1, "a");
        var b = dictionary.Add(2, "b");
        var countAtEnd = -1;
        a.Listen(() => countAtEnd = dictionary.Count);

        dictionary.Dispose();

        a.IsTerminated.Should().BeTrue();
        b.IsTerminated.Should().BeTrue();
        countAtEnd.Should().Be(0);
        dictionary.Should().BeEmpty();
    }

    [Fact]
    public void Dictionary_Advise_SeesExistingAndAdded()
    {
        var dictionary = new ViewableDictionary<int, string>();
        dictionary.Add(1, "a");
        var seen = new List<(int, string)>();

        dictionary.Advise(new Lifetime(), (key, value) => seen.Add((key, value)));
        dictionary.Add(2, "b");

        seen.Should().Equal((1, "a"), (2, "b"));
    }

    [Fact]
    public void Dictionary_AddLifetimed_RemovesWithTheLifetime()
    {
        var dictionary = new ViewableDictionary<int, string>();
        var lifetime = new Lifetime();

        dictionary.AddLifetimed(lifetime, 1, "a");
        lifetime.Terminate();

        dictionary.Should().BeEmpty();
    }

    [Fact]
    public void EventSource_SameHandlerTwice_EndOfOneLifetimeRemovesOnlyItsSubscription()
    {
        var source = new EventSource();
        var first = new Lifetime();
        var calls = 0;
        Action handler = () => calls++;

        source.Advise(first, handler);
        source.Advise(new Lifetime(), handler);
        first.Terminate();
        source.Invoke();

        calls.Should().Be(1);
    }
}
