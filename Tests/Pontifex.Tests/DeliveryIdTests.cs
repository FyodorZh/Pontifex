namespace Pontifex.Tests
{
    public class DeliveryIdTests
    {
        [Test]
        public void Zero_IsId0()
        {
            Assert.That(DeliveryId.Zero.Id, Is.EqualTo(0));
        }

        [Test]
        public void Next_FromZero_Returns1()
        {
            Assert.That(DeliveryId.Zero.Next, Is.EqualTo(new DeliveryId(1)));
        }

        [Test]
        public void Next_From1_Returns2()
        {
            var id = new DeliveryId(1);
            Assert.That(id.Next, Is.EqualTo(new DeliveryId(2)));
        }

        [Test]
        public void Next_From65534_Returns65535()
        {
            var id = new DeliveryId(65534);
            Assert.That(id.Next, Is.EqualTo(new DeliveryId(65535)));
        }

        [Test]
        public void Next_From65535_WrapsTo1()
        {
            var id = new DeliveryId(65535);
            Assert.That(id.Next, Is.EqualTo(new DeliveryId(1)));
        }

        [Test]
        public void SequentialNext_WrapsCorrectly()
        {
            var id = new DeliveryId(65534);
            id = id.Next;
            Assert.That(id, Is.EqualTo(new DeliveryId(65535)));
            id = id.Next;
            Assert.That(id, Is.EqualTo(new DeliveryId(1)));
            id = id.Next;
            Assert.That(id, Is.EqualTo(new DeliveryId(2)));
        }

        [Test]
        public void Equals_SameId_ReturnsTrue()
        {
            Assert.That(new DeliveryId(42).Equals(new DeliveryId(42)), Is.True);
        }

        [Test]
        public void Equals_DifferentId_ReturnsFalse()
        {
            Assert.That(new DeliveryId(1).Equals(new DeliveryId(2)), Is.False);
        }

        [Test]
        public void OperatorEquals_Works()
        {
            Assert.That(new DeliveryId(100) == new DeliveryId(100), Is.True);
            Assert.That(new DeliveryId(100) != new DeliveryId(101), Is.True);
        }

        [Test]
        public void CompareTo_IdsInOrder_ReturnsNegative()
        {
            Assert.That(new DeliveryId(1).CompareTo(new DeliveryId(2)), Is.LessThan(0));
        }

        [Test]
        public void CompareTo_IdsReversed_ReturnsPositive()
        {
            Assert.That(new DeliveryId(5).CompareTo(new DeliveryId(3)), Is.GreaterThan(0));
        }

        [Test]
        public void CompareTo_SameId_ReturnsZero()
        {
            Assert.That(new DeliveryId(10).CompareTo(new DeliveryId(10)), Is.EqualTo(0));
        }

        [Test]
        public void CompareTo_WrapAround_LowAfterHigh_ReturnsPositive()
        {
            Assert.That(new DeliveryId(1).CompareTo(new DeliveryId(65535)), Is.GreaterThan(0));
        }

        [Test]
        public void CompareTo_WrapAround_HighBeforeLow_ReturnsNegative()
        {
            Assert.That(new DeliveryId(65535).CompareTo(new DeliveryId(1)), Is.LessThan(0));
        }

        [Test]
        public void CompareTo_NearHalfBoundary_NoWrap()
        {
            Assert.That(new DeliveryId(32767).CompareTo(new DeliveryId(32768)), Is.LessThan(0));
            Assert.That(new DeliveryId(32768).CompareTo(new DeliveryId(32767)), Is.GreaterThan(0));
        }

        [Test]
        public void CompareTo_Zero_SentinelOrdering()
        {
            Assert.That(DeliveryId.Zero.CompareTo(new DeliveryId(1)), Is.LessThan(0));
        }

        [Test]
        public void GetHashCode_ReturnsId()
        {
            Assert.That(new DeliveryId(42).GetHashCode(), Is.EqualTo(42));
        }

        [Test]
        public void ToString_ReturnsIdString()
        {
            Assert.That(new DeliveryId(42).ToString(), Is.EqualTo("42"));
        }

        [Test]
        public void Subtraction_SimpleForward_ReturnsPositive()
        {
            Assert.That(new DeliveryId(5) - new DeliveryId(3), Is.EqualTo(2));
        }

        [Test]
        public void Subtraction_SimpleBackward_ReturnsNegative()
        {
            Assert.That(new DeliveryId(3) - new DeliveryId(5), Is.EqualTo(-2));
        }

        [Test]
        public void Subtraction_SameId_ReturnsZero()
        {
            Assert.That(new DeliveryId(100) - new DeliveryId(100), Is.EqualTo(0));
        }

        [Test]
        public void Subtraction_WrapAround_LowMinusHigh_ReturnsPositive()
        {
            Assert.That(new DeliveryId(1) - new DeliveryId(65535), Is.EqualTo(2));
        }

        [Test]
        public void Subtraction_WrapAround_HighMinusLow_ReturnsNegative()
        {
            Assert.That(new DeliveryId(65535) - new DeliveryId(1), Is.EqualTo(-2));
        }

        [Test]
        public void Subtraction_WithZero_Works()
        {
            Assert.That(new DeliveryId(10) - DeliveryId.Zero, Is.EqualTo(10));
            Assert.That(DeliveryId.Zero - new DeliveryId(10), Is.EqualTo(-10));
        }

        [Test]
        public void Subtraction_HalfBoundary()
        {
            Assert.That(new DeliveryId(32768) - DeliveryId.Zero, Is.EqualTo(-32768));
            Assert.That(DeliveryId.Zero - new DeliveryId(32768), Is.EqualTo(32768));
        }

        [Test]
        public void Subtraction_MatchesCompareToSign()
        {
            var pairs = new[]
            {
                (new DeliveryId(10), new DeliveryId(5)),
                (new DeliveryId(5), new DeliveryId(10)),
                (new DeliveryId(1), new DeliveryId(65535)),
                (new DeliveryId(65535), new DeliveryId(1)),
                (new DeliveryId(32767), new DeliveryId(32768)),
                (new DeliveryId(32768), new DeliveryId(32767)),
            };

            foreach (var (a, b) in pairs)
            {
                int sub = a - b;
                int cmp = a.CompareTo(b);
                Assert.That(Math.Sign(sub), Is.EqualTo(cmp),
                    $"Sign of ({a} - {b}) = {Math.Sign(sub)} should match CompareTo = {cmp}");
            }
        }
    }
}
