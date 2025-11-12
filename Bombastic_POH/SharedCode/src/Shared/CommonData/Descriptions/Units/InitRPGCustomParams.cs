using Serializer.BinarySerializer;

namespace Shared
{
    namespace CommonData
    {
        public class InitRPGCustomParams : IDataStruct
        {
            private AgentInertionParams mInetionParams;

            public AgentInertionParams InetionParams { get { return mInetionParams; } set { mInetionParams = value; } }

            public bool Serialize(IBinarySerializer dst)
            {
                float jumpCountMax = mInetionParams.JumpCountMax;
                dst.Add(ref jumpCountMax);

                float jumpFirstHeight = mInetionParams.JumpFirstHeight;
                dst.Add(ref jumpFirstHeight);

                float jumpHeight = mInetionParams.JumpHeight;
                dst.Add(ref jumpHeight);

                float horizontalMoveMaxAngle = mInetionParams.HorizontalMoveMaxAngle;
                dst.Add(ref horizontalMoveMaxAngle);

                mInetionParams = new AgentInertionParams
                {
                    GroundForwardMove = SerializeDirectionState(dst, mInetionParams.GroundForwardMove),
                    GroundBacwardMove = SerializeDirectionState(dst, mInetionParams.GroundBacwardMove),
                    GroundCrouchForwardMove = SerializeDirectionState(dst, mInetionParams.GroundCrouchForwardMove),
                    GroundCrouchBacwardMove = SerializeDirectionState(dst, mInetionParams.GroundCrouchBacwardMove),
                    AirForwardMove = SerializeDirectionState(dst, mInetionParams.AirForwardMove),
                    AirBackwardMove = SerializeDirectionState(dst, mInetionParams.AirBackwardMove),
                    AirUpFirstMove = SerializeDirectionState(dst, mInetionParams.AirUpFirstMove),
                    AirUpMove = SerializeDirectionState(dst, mInetionParams.AirUpMove),
                    AirDownMove = SerializeDirectionState(dst, mInetionParams.AirDownMove),
                    JumpCountMax = jumpCountMax,
                    JumpFirstHeight = jumpFirstHeight,
                    JumpHeight = jumpHeight,
                    HorizontalMoveMaxAngle = horizontalMoveMaxAngle
                };
                mInetionParams.Init();

                return true;
            }

            private DirectionStateParams SerializeDirectionState(IBinarySerializer dst, DirectionStateParams data)
            {
                float speedMax = data.SpeedMax;
                dst.Add(ref speedMax);

                float accelerationTime = data.AccelerationTime;
                dst.Add(ref accelerationTime);

                float stopTime = data.StopTime;
                dst.Add(ref stopTime);

                return new DirectionStateParams { SpeedMax = speedMax, AccelerationTime = accelerationTime, StopTime = stopTime };
            }
        }
    }
}
