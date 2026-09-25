using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

public class TypingBurstTests
{
    [Fact]
    public void PhysicalTextInputBuffer_PreservesRapidKeydownOrder()
    {
        long now = 100;
        var buffer = new PhysicalTextInputBuffer(1000, () => now);

        buffer.Enqueue("a");
        buffer.Enqueue("b");
        buffer.Enqueue("cd");

        Assert.Equal('a', buffer.Resolve('x'));
        Assert.Equal('b', buffer.Resolve('x'));
        Assert.Equal('c', buffer.Resolve('x'));
        Assert.Equal('d', buffer.Resolve('x'));
        Assert.Equal('x', buffer.Resolve('x'));
    }

    [Fact]
    public void PhysicalTextInputBuffer_DiscardsExpiredEntries()
    {
        long now = 100;
        var buffer = new PhysicalTextInputBuffer(1000, () => now);
        buffer.Enqueue("a");

        now = 1101;

        Assert.Equal('x', buffer.Resolve('x'));
    }

    [Fact]
    public void PhysicalTextInputBuffer_DiscardsExpiredEntriesBeforeNewKeydown()
    {
        long now = 100;
        var buffer = new PhysicalTextInputBuffer(1000, () => now);
        buffer.Enqueue("a");

        now = 1101;
        buffer.Enqueue("b");

        Assert.Equal('b', buffer.Resolve('x'));
        Assert.Equal('x', buffer.Resolve('x'));
    }

    [Fact]
    public void TypingSession_AdvancesMultilineExerciseWithoutInputGap()
    {
        var exercise = new LessonExercise(
            "m", "l", 0, "practice", "Tape", "ab\ncd", LessonTypingMode.Strict);
        var session = new LessonTypingSession(exercise);

        Assert.True(session.TypeCharAndAdvanceLine('a').Accepted);
        var firstLine = session.TypeCharAndAdvanceLine('b');

        Assert.True(firstLine.LineCompleted);
        Assert.False(firstLine.ExerciseCompleted);
        Assert.False(session.IsLineComplete);
        Assert.Equal(1, session.LineIndex);
        Assert.True(session.TypeCharAndAdvanceLine('c').Accepted);
        Assert.True(session.TypeCharAndAdvanceLine('d').ExerciseCompleted);
    }
}
