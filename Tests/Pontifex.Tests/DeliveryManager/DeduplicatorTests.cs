namespace Pontifex.DeliveryManager.Tests
{
    [Category("DeliveryManager")]
    public class DeduplicatorTests
    {
        [Test]
        public void FirstId_ReturnsNew()
        {
            var dedup = new Deduplicator(8);
            Assert.That(dedup.Received(1), Is.EqualTo(Deduplicator.Result.New));
        }

        [Test]
        public void SequentialIds_AllNew()
        {
            var dedup = new Deduplicator(16);
            for (uint i = 1; i <= 5; i++)
                Assert.That(dedup.Received(i), Is.EqualTo(Deduplicator.Result.New));
        }

        [Test]
        public void SameIdTwice_ReturnsDuplicate()
        {
            var dedup = new Deduplicator(8);
            dedup.Received(1);
            Assert.That(dedup.Received(1), Is.EqualTo(Deduplicator.Result.Duplicate));
        }

        [Test]
        public void IdInWindow_AlreadyReceived_ReturnsDuplicate()
        {
            var dedup = new Deduplicator(16);
            dedup.Received(1);
            dedup.Received(2);
            dedup.Received(3);
            Assert.That(dedup.Received(2), Is.EqualTo(Deduplicator.Result.Duplicate));
        }

        [Test]
        public void IdBelowFrom_ReturnsDuplicate()
        {
            var dedup = new Deduplicator(8);
            for (uint i = 1; i <= 8; i++) dedup.Received(i);
            dedup.Received(9);
            Assert.That(dedup.Received(4), Is.EqualTo(Deduplicator.Result.Duplicate));
        }

        [Test]
        public void GapFromEmpty_FillsAndMarksNew()
        {
            var dedup = new Deduplicator(16);
            dedup.Received(1);
            Assert.That(dedup.Received(5), Is.EqualTo(Deduplicator.Result.New));
        }

        [Test]
        public void Overflow_WhenGapExceedsCapacity()
        {
            var dedup = new Deduplicator(4);
            dedup.Received(1);
            var result = dedup.Received(10);
            Assert.That(result, Is.EqualTo(Deduplicator.Result.Overflow));
        }

        [Test]
        public void Trim_RemovesLeadingConfirmed()
        {
            var dedup = new Deduplicator(8);
            for (uint i = 1; i <= 8; i++) dedup.Received(i);
            dedup.Received(9);
            Assert.That(dedup.Received(9), Is.EqualTo(Deduplicator.Result.Duplicate));
            Assert.That(dedup.Received(10), Is.EqualTo(Deduplicator.Result.New));
        }

        [Test]
        public void LargeSequentialRange_AllNew()
        {
            var dedup = new Deduplicator(1024);
            for (uint i = 1; i <= 100; i++)
                Assert.That(dedup.Received(i), Is.EqualTo(Deduplicator.Result.New));
        }

        [Test]
        public void RapidSameId_StaysDuplicate()
        {
            var dedup = new Deduplicator(8);
            dedup.Received(1);
            for (int i = 0; i < 10; i++)
                Assert.That(dedup.Received(1), Is.EqualTo(Deduplicator.Result.Duplicate));
        }

        [Test]
        public void NewIdAfterTrim_Works()
        {
            var dedup = new Deduplicator(4);
            dedup.Received(1);
            dedup.Received(2);
            dedup.Received(3);
            dedup.Received(4);
            dedup.Received(5);
            dedup.Received(6);
            dedup.Received(7);
            Assert.That(dedup.Received(8), Is.EqualTo(Deduplicator.Result.New));
            Assert.That(dedup.Received(2), Is.EqualTo(Deduplicator.Result.Duplicate));
        }

        [Test]
        public void OverflowAtFullCapacity_ReturnsOverflow()
        {
            var dedup = new Deduplicator(4);
            dedup.Received(1);
            dedup.Received(2);
            dedup.Received(3);
            dedup.Received(4);
            var result = dedup.Received(100);
            Assert.That(result, Is.EqualTo(Deduplicator.Result.Overflow));
        }
    }
}
