using Pontifex.DeliveryManager;

namespace Pontifex.DeliveryManager.Tests
{
    [Category("DeliveryManager")]
    public class DeliverySorterTests
    {
        private DeliverySorter<string> Create() => new DeliverySorter<string>(DeliveryId.Zero);

        [Test]
        public void FirstPush_SetsExpectedId()
        {
            var sorter = Create();
            Assert.That(sorter.Push(new DeliveryId(5), "five"), Is.True);
            Assert.That(sorter.TryPop(out var id, out var data), Is.True);
            Assert.That(id, Is.EqualTo(new DeliveryId(5)));
            Assert.That(data, Is.EqualTo("five"));
        }

        [Test]
        public void SequentialPushPop_Works()
        {
            var sorter = Create();
            sorter.Push(new DeliveryId(1), "a");
            sorter.Push(new DeliveryId(2), "b");

            Assert.That(sorter.TryPop(out var id1, out _), Is.True);
            Assert.That(id1, Is.EqualTo(new DeliveryId(1)));

            Assert.That(sorter.TryPop(out var id2, out _), Is.True);
            Assert.That(id2, Is.EqualTo(new DeliveryId(2)));
        }

        [Test]
        public void AheadIdsBuffered_PoppedInOrder()
        {
            var sorter = Create();
            sorter.Push(new DeliveryId(1), "a");
            sorter.Push(new DeliveryId(3), "c");
            sorter.Push(new DeliveryId(2), "b");

            Assert.That(sorter.TryPop(out var id1, out _), Is.True);
            Assert.That(id1.Id, Is.EqualTo(1));

            Assert.That(sorter.TryPop(out var id2, out _), Is.True);
            Assert.That(id2.Id, Is.EqualTo(2));

            Assert.That(sorter.TryPop(out var id3, out _), Is.True);
            Assert.That(id3.Id, Is.EqualTo(3));
        }

        [Test]
        public void TryPop_WhenNextMissing_ReturnsFalse()
        {
            var sorter = Create();
            sorter.Push(new DeliveryId(1), "a");
            sorter.TryPop(out _, out _);
            sorter.Push(new DeliveryId(3), "c");

            Assert.That(sorter.TryPop(out _, out _), Is.False);
        }

        [Test]
        public void PastId_Rejected()
        {
            var sorter = Create();
            sorter.Push(new DeliveryId(5), "five");
            sorter.TryPop(out _, out _);

            Assert.That(sorter.Push(new DeliveryId(3), "three"), Is.False);
        }

        [Test]
        public void AfterClear_PushReturnsFalse()
        {
            var sorter = Create();
            sorter.Clear(_ => { });

            Assert.That(sorter.Push(new DeliveryId(1), "a"), Is.False);
        }

        [Test]
        public void AfterClear_TryPopReturnsFalse()
        {
            var sorter = Create();
            sorter.Clear(_ => { });

            Assert.That(sorter.TryPop(out _, out _), Is.False);
        }

        [Test]
        public void Clear_ReleasesAllAndSetsError()
        {
            var sorter = Create();
            sorter.Push(new DeliveryId(1), "a");
            sorter.Push(new DeliveryId(2), "b");

            int released = 0;
            sorter.Clear(_ => released++);

            Assert.That(released, Is.EqualTo(2));
            Assert.That(sorter.Push(new DeliveryId(3), "c"), Is.False);
        }

        [Test]
        public void FirstMessageLowerThanStartId_Accepted()
        {
            var sorter = new DeliverySorter<string>(new DeliveryId(100));
            Assert.That(sorter.Push(new DeliveryId(50), "fifty"), Is.True);
        }

        [Test]
        public void EmptyAfterAllPops()
        {
            var sorter = Create();
            sorter.Push(new DeliveryId(1), "a");
            sorter.TryPop(out _, out _);
            Assert.That(sorter.TryPop(out _, out _), Is.False);
        }

        [Test]
        public void GapFill_ThenPopMultiple()
        {
            var sorter = Create();
            sorter.Push(new DeliveryId(1), "a");
            sorter.TryPop(out _, out _);

            sorter.Push(new DeliveryId(3), "c");
            sorter.Push(new DeliveryId(4), "d");

            Assert.That(sorter.TryPop(out _, out _), Is.False);

            sorter.Push(new DeliveryId(2), "b");

            Assert.That(sorter.TryPop(out var id2, out _), Is.True);
            Assert.That(id2.Id, Is.EqualTo(2));

            Assert.That(sorter.TryPop(out var id3, out _), Is.True);
            Assert.That(id3.Id, Is.EqualTo(3));

            Assert.That(sorter.TryPop(out var id4, out _), Is.True);
            Assert.That(id4.Id, Is.EqualTo(4));
        }

        [Test]
        public void SameIdAfterPop_AcceptedForNextSequence()
        {
            var sorter = Create();
            sorter.Push(new DeliveryId(5), "a");
            sorter.TryPop(out _, out _);

            Assert.That(sorter.Push(new DeliveryId(5), "b"), Is.False);
            Assert.That(sorter.Push(new DeliveryId(6), "c"), Is.True);
        }
    }
}
