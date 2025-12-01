// using System;
// using System.Threading;
// using Archivarius.UnionDataListBackend;
// using NewProtocol;
// using NewProtocol.Client;
// using Shared;
// using Transport;
// using Transport.Abstractions.Clients;
//
// namespace TransportAnalyzer.TestLogic
// {
//     class AckRawTestClient : AckRawClientProtocol
//     {
//         private readonly int mUnconfirmedTicks;
//         private readonly long mLastTickId;
//
//         private long mSendId = 0;
//         private long mReceiveId = 0;
//
//         private readonly Action<IRequestSuccess<BytesBuffer>> mOk;
//         private readonly Action<IRequestFail> mFail;
//
//         private AckRawProtocol mProtocol;
//
//         public event Action Started;
//         public event Action<StopReason> Stopped;
//
//         private int mStage = 0;
//
//         public AckRawTestClient(IAckRawClient transport, int unconfirmedTicks = 1, long lastTickId = -1)
//             : base(transport, new ModelsHashDB(), UtcNowDateTimeProvider.Instance)
//         {
//             mUnconfirmedTicks = unconfirmedTicks;
//             mLastTickId = lastTickId;
//
//             mOk = Ok;
//             mFail = Fail;
//         }
//
//         protected override Protocol ConstructProtocol()
//         {
//             mProtocol = new AckRawProtocol();
//             mProtocol.OnAck.Register((bytes) => {
//                 if (System.Threading.Interlocked.Increment(ref mStage) != 2)
//                 {
//                     Log.e("Wrong stage (B)");
//                 }
//             });
//             return mProtocol;
//         }
//
//         protected override void OnStopped(StopReason reason)
//         {
//             if (Stopped != null)
//             {
//                 Stopped(reason);
//             }
//         }
//
//         protected override void WriteAckData(UnionDataList ackData)
//         {
//             ackData.PutFirst("AckRawTestProtocol");
//         }
//
//         protected override bool OnConnecting(bool protocolIsValid, ByteArraySegment ackResponse)
//         {
//             if (System.Threading.Interlocked.Increment(ref mStage) != 1)
//             {
//                 Log.e("Wrong stage (A)");
//             }
//
//             if (!AckUtils.CheckPrefix(ackResponse, "OK").IsValid)
//             {
//                 Log.e("Wrong ack response");
//                 return false;
//             }
//             if (!protocolIsValid)
//             {
//                 Log.e("Wrong protocol");
//             }
//             if (protocolIsValid && Started != null)
//             {
//                 Started();
//             }
//             return protocolIsValid;
//         }
//
//         protected override void OnTick(DateTime now)
//         {
//             if (IsConnected)
//             {
//                 if (mSendId - mReceiveId < mUnconfirmedTicks)
//                 {
//                     var data = AckRawCommonLogic.GenBuffer(Interlocked.Increment(ref mSendId));
//                     mProtocol.Request.Request(new BytesBuffer {Data = data}, mOk, mFail);
//                 }
//             }
//
//             base.OnTick(now);
//         }
//
//         private void Ok(IRequestSuccess<BytesBuffer> response)
//         {
//             if (mStage != 2)
//             {
//                 Log.e("Wrong stage (C)");
//             }
//
//             var id = Interlocked.Increment(ref mReceiveId);
//             if (!AckRawCommonLogic.CheckBuffer(id, new ByteArraySegment(response.Response.Data)) || id == mLastTickId)
//             {
//                 Log.e("Message check failed #" + id);
//                 Stop();
//             }
//         }
//
//         private void Fail(IRequestFail response)
//         {
//             Log.e("Response failed");
//             GracefulStop(DeltaTime.FromSeconds(10));
//         }
//
//         public override string ToString()
//         {
//             return "Client[" + mReceiveId + "]";
//         }
//     }
// }