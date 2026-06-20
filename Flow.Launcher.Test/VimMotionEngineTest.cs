using NUnit.Framework;
using Flow.Launcher.VimMode;

namespace Flow.Launcher.Test
{
    [TestFixture]
    public class VimMotionEngineTest
    {
        private const string Sample = "hello world_foo bar";

        [Test]
        public void MoveLeftTest()
        {
            Assert.That(VimMotionEngine.MoveLeft(5), Is.EqualTo(4));
            Assert.That(VimMotionEngine.MoveLeft(0), Is.EqualTo(0));
            Assert.That(VimMotionEngine.MoveLeft(2), Is.EqualTo(1));
        }

        [Test]
        public void MoveRightTest()
        {
            Assert.That(VimMotionEngine.MoveRight(5, Sample.Length), Is.EqualTo(6));
            Assert.That(VimMotionEngine.MoveRight(Sample.Length, Sample.Length), Is.EqualTo(Sample.Length));
            Assert.That(VimMotionEngine.MoveRight(Sample.Length - 1, Sample.Length), Is.EqualTo(Sample.Length));
        }

        [Test]
        public void MoveStartOfLineTest()
        {
            Assert.That(VimMotionEngine.MoveStartOfLine(), Is.EqualTo(0));
        }

        [Test]
        public void MoveEndOfLineTest()
        {
            Assert.That(VimMotionEngine.MoveEndOfLine(Sample.Length), Is.EqualTo(Sample.Length));
            Assert.That(VimMotionEngine.MoveEndOfLine(0), Is.EqualTo(0));
        }

        [Test]
        public void MoveFirstNonBlankTest()
        {
            Assert.That(VimMotionEngine.MoveFirstNonBlank("   hello"), Is.EqualTo(3));
            Assert.That(VimMotionEngine.MoveFirstNonBlank("hello"), Is.EqualTo(0));
            Assert.That(VimMotionEngine.MoveFirstNonBlank("    "), Is.EqualTo(0));
            Assert.That(VimMotionEngine.MoveFirstNonBlank(""), Is.EqualTo(0));
        }

        [Test]
        public void MoveNextWordTest()
        {
            Assert.That(VimMotionEngine.MoveNextWord(Sample, 0), Is.EqualTo(6));
            Assert.That(VimMotionEngine.MoveNextWord(Sample, 6), Is.EqualTo(16));
            Assert.That(VimMotionEngine.MoveNextWord(Sample, Sample.Length), Is.EqualTo(Sample.Length));
            Assert.That(VimMotionEngine.MoveNextWord("   abc", 0), Is.EqualTo(3));
        }

        [Test]
        public void MovePrevWordTest()
        {
            Assert.That(VimMotionEngine.MovePrevWord(Sample, 6), Is.EqualTo(0));
            Assert.That(VimMotionEngine.MovePrevWord("hello   world", 8), Is.EqualTo(0));
            Assert.That(VimMotionEngine.MovePrevWord(Sample, 0), Is.EqualTo(0));
        }

        [Test]
        public void MoveEndWordTest()
        {
            Assert.That(VimMotionEngine.MoveEndWord(Sample, 0), Is.EqualTo(4));
            Assert.That(VimMotionEngine.MoveEndWord(Sample, 5), Is.EqualTo(14));
            Assert.That(VimMotionEngine.MoveEndWord(Sample, Sample.Length - 1), Is.EqualTo(Sample.Length - 1));
        }

        [Test]
        public void FindCharForwardTest()
        {
            Assert.That(VimMotionEngine.FindCharForward(Sample, 0, 'o', false), Is.EqualTo(4));
            Assert.That(VimMotionEngine.FindCharForward(Sample, 0, 'o', till: true), Is.EqualTo(3));
            Assert.That(VimMotionEngine.FindCharForward(Sample, 0, 'z', false), Is.EqualTo(0));
            Assert.That(VimMotionEngine.FindCharForward(Sample, Sample.Length - 1, 'o', false), Is.EqualTo(Sample.Length - 1));
            Assert.That(VimMotionEngine.FindCharForward(Sample, 4, 'o', false), Is.EqualTo(7));
        }

        [Test]
        public void FindCharBackwardTest()
        {
            Assert.That(VimMotionEngine.FindCharBackward(Sample, 7, 'o', false), Is.EqualTo(4));
            Assert.That(VimMotionEngine.FindCharBackward(Sample, 7, 'o', till: true), Is.EqualTo(5));
            Assert.That(VimMotionEngine.FindCharBackward(Sample, Sample.Length - 1, 'z', false), Is.EqualTo(Sample.Length - 1));
            Assert.That(VimMotionEngine.FindCharBackward(Sample, 0, 'o', false), Is.EqualTo(0));
            Assert.That(VimMotionEngine.FindCharBackward(Sample, 4, 'o', false), Is.EqualTo(4));
        }

        [Test]
        public void MoveEndWordEmptyOrEndDoesNotReturnNegative()
        {
            Assert.That(VimMotionEngine.MoveEndWord("", 0), Is.EqualTo(0));
            Assert.That(VimMotionEngine.MoveEndWordBig("", 0), Is.EqualTo(0));
            Assert.That(VimMotionEngine.MoveEndWord("a", 0), Is.EqualTo(0));
        }

        [Test]
        public void MoveLastNonBlankTest()
        {
            Assert.That(VimMotionEngine.MoveLastNonBlank("hello  "), Is.EqualTo(5));
            Assert.That(VimMotionEngine.MoveLastNonBlank("hello"), Is.EqualTo(5));
            Assert.That(VimMotionEngine.MoveLastNonBlank("   "), Is.EqualTo(0));
            Assert.That(VimMotionEngine.MoveLastNonBlank(""), Is.EqualTo(0));
        }

        [Test]
        public void MoveNextWordBigTest()
        {
            Assert.That(VimMotionEngine.MoveNextWordBig("foo bar baz", 0), Is.EqualTo(4));
            Assert.That(VimMotionEngine.MoveNextWordBig("foo bar baz", 4), Is.EqualTo(8));
            // Punctuation is part of a BIG word (unlike w).
            Assert.That(VimMotionEngine.MoveNextWordBig("foo.bar baz", 0), Is.EqualTo(8));
            Assert.That(VimMotionEngine.MoveNextWordBig("foo", 3), Is.EqualTo(3));
        }

        [Test]
        public void MovePrevWordBigTest()
        {
            Assert.That(VimMotionEngine.MovePrevWordBig("foo bar baz", 8), Is.EqualTo(4));
            Assert.That(VimMotionEngine.MovePrevWordBig("foo bar", 4), Is.EqualTo(0));
            Assert.That(VimMotionEngine.MovePrevWordBig("foo", 0), Is.EqualTo(0));
        }

        [Test]
        public void MoveEndWordBigTest()
        {
            Assert.That(VimMotionEngine.MoveEndWordBig("foo bar", 0), Is.EqualTo(2));
            Assert.That(VimMotionEngine.MoveEndWordBig("foo bar", 2), Is.EqualTo(6));
        }

        [Test]
        public void MoveToMatchingBracketTest()
        {
            Assert.That(VimMotionEngine.MoveToMatchingBracket("a(bc)d", 1), Is.EqualTo(4));
            Assert.That(VimMotionEngine.MoveToMatchingBracket("a(bc)d", 4), Is.EqualTo(1));
            Assert.That(VimMotionEngine.MoveToMatchingBracket("(a(b)c)", 0), Is.EqualTo(6));
            // No bracket under the caret: stay put.
            Assert.That(VimMotionEngine.MoveToMatchingBracket("abc", 1), Is.EqualTo(1));
        }

        [Test]
        public void TextObjectWordTest()
        {
            Assert.That(VimMotionEngine.TextObjectWord("foo bar", 1, false), Is.EqualTo((0, 2)));
            // aw includes the trailing whitespace.
            Assert.That(VimMotionEngine.TextObjectWord("foo bar", 1, true), Is.EqualTo((0, 3)));
            Assert.That(VimMotionEngine.TextObjectWord("foo bar", 5, false), Is.EqualTo((4, 6)));
            // No trailing whitespace at end of string -> aw extends over leading whitespace instead.
            Assert.That(VimMotionEngine.TextObjectWord("foo bar", 5, true), Is.EqualTo((3, 6)));
            Assert.That(VimMotionEngine.TextObjectWord("", 0, false), Is.EqualTo((0, 0)));
        }

        [Test]
        public void TextObjectQuoteTest()
        {
            Assert.That(VimMotionEngine.TextObjectQuote("say \"hi\" now", 5, '"', false), Is.EqualTo((5, 6)));
            Assert.That(VimMotionEngine.TextObjectQuote("say \"hi\" now", 5, '"', true), Is.EqualTo((4, 7)));
            Assert.That(VimMotionEngine.TextObjectQuote("hello", 1, '"', false), Is.EqualTo((-1, -1)));
        }

        [Test]
        public void TextObjectDelimitedTest()
        {
            Assert.That(VimMotionEngine.TextObjectDelimited("f(a, b)", 3, '(', ')', false), Is.EqualTo((2, 5)));
            Assert.That(VimMotionEngine.TextObjectDelimited("f(a, b)", 3, '(', ')', true), Is.EqualTo((1, 6)));
            // Nested: caret inside the inner pair selects the inner pair.
            Assert.That(VimMotionEngine.TextObjectDelimited("(a(b)c)", 3, '(', ')', false), Is.EqualTo((3, 3)));
            Assert.That(VimMotionEngine.TextObjectDelimited("(a(b)c)", 3, '(', ')', true), Is.EqualTo((2, 4)));
            Assert.That(VimMotionEngine.TextObjectDelimited("abc", 1, '(', ')', false), Is.EqualTo((-1, -1)));
        }

        [Test]
        public void OperatorRangeExclusiveTest()
        {
            // dw etc.: half-open, destination not included, in either direction.
            Assert.That(VimMotionEngine.OperatorRange(0, 3, MotionInclusivity.Exclusive, 10), Is.EqualTo((0, 3)));
            Assert.That(VimMotionEngine.OperatorRange(5, 2, MotionInclusivity.Exclusive, 10), Is.EqualTo((2, 5)));
        }

        [Test]
        public void OperatorRangeInclusiveForwardTest()
        {
            // de / df: forward motion consumes the destination character.
            Assert.That(VimMotionEngine.OperatorRange(0, 3, MotionInclusivity.InclusiveForward, 10), Is.EqualTo((0, 4)));
            // At the last index it still extends to the end of the text.
            Assert.That(VimMotionEngine.OperatorRange(0, 9, MotionInclusivity.InclusiveForward, 10), Is.EqualTo((0, 10)));
            // Never extends past the text length.
            Assert.That(VimMotionEngine.OperatorRange(0, 10, MotionInclusivity.InclusiveForward, 10), Is.EqualTo((0, 10)));
            // Backward find (F/T): the original caret char is NOT consumed.
            Assert.That(VimMotionEngine.OperatorRange(5, 2, MotionInclusivity.InclusiveForward, 10), Is.EqualTo((2, 5)));
            // Failed find (target == caret): empty range, nothing deleted.
            Assert.That(VimMotionEngine.OperatorRange(5, 5, MotionInclusivity.InclusiveForward, 10), Is.EqualTo((5, 5)));
        }

        [Test]
        public void OperatorRangeInclusivePairTest()
        {
            // d% with caret on '(': forward, both brackets included.
            Assert.That(VimMotionEngine.OperatorRange(1, 4, MotionInclusivity.InclusivePair, 6), Is.EqualTo((1, 5)));
            // d% with caret on ')': backward, the far-end (the ')') is still included.
            Assert.That(VimMotionEngine.OperatorRange(4, 0, MotionInclusivity.InclusivePair, 6), Is.EqualTo((0, 5)));
            // No matching bracket (target == caret): empty range, nothing deleted.
            Assert.That(VimMotionEngine.OperatorRange(3, 3, MotionInclusivity.InclusivePair, 10), Is.EqualTo((3, 3)));
        }
    }
}
