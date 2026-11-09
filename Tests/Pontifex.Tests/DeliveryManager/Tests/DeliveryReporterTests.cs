using System.Collections.Generic;
using Actuarius.Collections;
using Pontifex.Delivery;
using Pontifex.Tests.Delivery.Harness;
using Pontifex.Utils;

namespace Pontifex.Tests.Delivery
{
    [TestFixture]
    [Category("DeliveryManager")]
    public sealed class DeliveryReporterTests : TrackedFixture
    {
        private static DeliveryInfo Info(ushort id, byte chunk = 0) => new DeliveryInfo(new DeliveryId(id), chunk);

        private List<UnionDataList> Flush(DeliveryReporter reporter, int messageMaxByteSize)
        {
            var sent = new List<UnionDataList>();
            var serializer = new DeliveryInfoSerializer(CPool);
            reporter.Flush(serializer, messageMaxByteSize, 4,
                new ConsumerDelegate<UnionDataList>(x => { sent.Add(x); TrackCaptured(x); return true; }));
            return sent;
        }

        [Test]
        public void Flush_WhenEmpty_ProducesNothing()
        {
            var reporter = new DeliveryReporter();

            Assert.That(Flush(reporter, 100), Is.Empty);
        }

        [Test]
        public void Flush_SingleReport_StartsWithDiscriminatorAndClears()
        {
            var reporter = new DeliveryReporter();
            reporter.Add(Info(1));
            reporter.Add(Info(2, 3));

            var sent = Flush(reporter, 100);

            Assert.That(sent, Has.Count.EqualTo(1));
            Assert.That(sent[0].Elements[0].Alias.BoolValue, Is.False);
            Assert.That(sent[0].Elements[1].Alias.UShortValue, Is.EqualTo(2));
            Assert.That(Flush(reporter, 100), Is.Empty);
        }

        [Test]
        public void Flush_WhenConfirmationsExceedPacket_SplitsIntoBatches()
        {
            var reporter = new DeliveryReporter();
            for (ushort id = 1; id <= 5; id++)
            {
                reporter.Add(Info(id));
            }

            // (19 - 4 fixed overhead - 4 safety margin) / 5 bytes per confirmation = 2 per packet.
            var sent = Flush(reporter, 19);

            Assert.That(sent, Has.Count.EqualTo(3));
            Assert.That(sent[0].Elements[1].Alias.UShortValue, Is.EqualTo(2));
            Assert.That(sent[1].Elements[1].Alias.UShortValue, Is.EqualTo(2));
            Assert.That(sent[2].Elements[1].Alias.UShortValue, Is.EqualTo(1));
            foreach (var message in sent)
            {
                Assert.That(message.Elements[0].Alias.BoolValue, Is.False);
            }
        }

        [Test]
        public void Clear_DropsPendingConfirmations()
        {
            var reporter = new DeliveryReporter();
            reporter.Add(Info(1));

            reporter.Clear();

            Assert.That(Flush(reporter, 100), Is.Empty);
        }
    }
}
