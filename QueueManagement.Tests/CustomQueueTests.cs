using QueueManagement.Core;
using Xunit;

namespace QueueManagement.Tests;

public class CustomQueueTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_CreatesEmptyQueue()
    {
        var queue = new CustomQueue<int>();

        Assert.True(queue.IsEmpty());
        Assert.Equal(0, queue.Count);
    }

    #endregion

    #region Enqueue Tests

    [Fact]
    public void Enqueue_SingleElement_IncreasesCount()
    {
        var queue = new CustomQueue<string>();

        queue.Enqueue("first");

        Assert.Equal(1, queue.Count);
        Assert.False(queue.IsEmpty());
    }

    [Fact]
    public void Enqueue_MultipleElements_MaintainsFIFOOrder()
    {
        var queue = new CustomQueue<int>();

        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Enqueue(3);

        Assert.Equal(3, queue.Count);
        Assert.Equal(1, queue.Peek());
    }

    [Fact]
    public void Enqueue_NullElement_Succeeds()
    {
        var queue = new CustomQueue<string?>();

        queue.Enqueue(null);

        Assert.Equal(1, queue.Count);
        Assert.Null(queue.Peek());
    }

    #endregion

    #region Dequeue Tests

    [Fact]
    public void Dequeue_ReturnsFirstElement()
    {
        var queue = new CustomQueue<string>();
        queue.Enqueue("first");
        queue.Enqueue("second");

        var result = queue.Dequeue();

        Assert.Equal("first", result);
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public void Dequeue_EmptyQueue_ThrowsInvalidOperationException()
    {
        var queue = new CustomQueue<int>();

        Assert.Throws<InvalidOperationException>(() => queue.Dequeue());
    }

    [Fact]
    public void Dequeue_AllElements_LeavesEmptyQueue()
    {
        var queue = new CustomQueue<int>();
        queue.Enqueue(1);
        queue.Enqueue(2);

        queue.Dequeue();
        queue.Dequeue();

        Assert.True(queue.IsEmpty());
        Assert.Equal(0, queue.Count);
    }

    #endregion

    #region Peek Tests

    [Fact]
    public void Peek_ReturnsFirstElementWithoutRemoving()
    {
        var queue = new CustomQueue<string>();
        queue.Enqueue("first");
        queue.Enqueue("second");

        var result = queue.Peek();

        Assert.Equal("first", result);
        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public void Peek_EmptyQueue_ThrowsInvalidOperationException()
    {
        var queue = new CustomQueue<int>();

        Assert.Throws<InvalidOperationException>(() => queue.Peek());
    }

    [Fact]
    public void Peek_MultipleCalls_ReturnsSameElement()
    {
        var queue = new CustomQueue<int>();
        queue.Enqueue(42);

        var first = queue.Peek();
        var second = queue.Peek();

        Assert.Equal(first, second);
        Assert.Equal(1, queue.Count);
    }

    #endregion

    #region IsEmpty Tests

    [Fact]
    public void IsEmpty_NewQueue_ReturnsTrue()
    {
        var queue = new CustomQueue<int>();

        Assert.True(queue.IsEmpty());
    }

    [Fact]
    public void IsEmpty_AfterEnqueue_ReturnsFalse()
    {
        var queue = new CustomQueue<int>();
        queue.Enqueue(1);

        Assert.False(queue.IsEmpty());
    }

    [Fact]
    public void IsEmpty_AfterDequeueAll_ReturnsTrue()
    {
        var queue = new CustomQueue<int>();
        queue.Enqueue(1);
        queue.Dequeue();

        Assert.True(queue.IsEmpty());
    }

    #endregion

    #region Count Tests

    [Fact]
    public void Count_EmptyQueue_ReturnsZero()
    {
        var queue = new CustomQueue<int>();

        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Count_AfterOperations_ReturnsCorrectValue()
    {
        var queue = new CustomQueue<int>();
        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Enqueue(3);
        queue.Dequeue();

        Assert.Equal(2, queue.Count);
    }

    #endregion

    #region Clear Tests

    [Fact]
    public void Clear_RemovesAllElements()
    {
        var queue = new CustomQueue<int>();
        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Enqueue(3);

        queue.Clear();

        Assert.True(queue.IsEmpty());
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Clear_EmptyQueue_Succeeds()
    {
        var queue = new CustomQueue<int>();

        queue.Clear();

        Assert.True(queue.IsEmpty());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_EmptyQueue_ReturnsEmptyFormat()
    {
        var queue = new CustomQueue<int>();

        var result = queue.ToString();

        Assert.Equal("Queue: []", result);
    }

    [Fact]
    public void ToString_WithElements_ReturnsFormattedString()
    {
        var queue = new CustomQueue<int>();
        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Enqueue(3);

        var result = queue.ToString();

        Assert.Equal("Queue: [1, 2, 3]", result);
    }

    #endregion

    #region Generic Type Tests

    [Fact]
    public void Queue_WithCustomType_WorksCorrectly()
    {
        var queue = new CustomQueue<Person>();
        var person1 = new Person("Alice", 30);
        var person2 = new Person("Bob", 25);

        queue.Enqueue(person1);
        queue.Enqueue(person2);

        Assert.Equal(person1, queue.Dequeue());
        Assert.Equal(person2, queue.Dequeue());
    }

    private record Person(string Name, int Age);

    #endregion
}
