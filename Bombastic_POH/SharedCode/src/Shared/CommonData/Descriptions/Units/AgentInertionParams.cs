using System;
using Geom2d;
using System.Xml.Linq;
using System.Diagnostics;

namespace Shared
{
    namespace CommonData
    {
        [System.Serializable]
        public struct DirectionStateParams
        {
            private const string DIRECTION_STATE_PARAMS = "DirectionStateParams";
            private const string SPEED_MAX = "SpeedMax";
            private const string ACCELERATION_TIME = "AccelerationTime";
            private const string STOP_TIME = "StopTime";

            public float SpeedMax;
            public float AccelerationTime;
            public float StopTime;

            public static DirectionStateParams Deserialize(StorageFolder from)
            {
                var data = from.GetFolder(DIRECTION_STATE_PARAMS);
                var speedMax = data.GetItemAsFloat(SPEED_MAX);
                var accelerationTime = data.GetItemAsFloat(ACCELERATION_TIME);
                var stopTime = data.GetItemAsFloat(STOP_TIME);

                return new DirectionStateParams()
                {
                    SpeedMax = speedMax,
                    AccelerationTime = accelerationTime,
                    StopTime = stopTime
                };
            }

            public StorageFolder Serialize()
            {
                var to = new StorageFolder(DIRECTION_STATE_PARAMS);

                to.AddItem(new StorageFloat(SPEED_MAX, SpeedMax));
                to.AddItem(new StorageFloat(ACCELERATION_TIME, AccelerationTime));
                to.AddItem(new StorageFloat(STOP_TIME, StopTime));

                return to;
            }

            public float GetStopSpeed(float deltaTime)
            {
                return StopTime == 0.0f ? SpeedMax : (SpeedMax / StopTime) * deltaTime;
            }

            public float GetAccelerateSpeed(float deltaTime)
            {
                return AccelerationTime == 0.0f ? SpeedMax : (SpeedMax / AccelerationTime) * deltaTime;
            }
        }

        [System.Serializable]
        public struct AgentInertionParams
        {
            private const string UNIT_INERTIAL_PARAMS = "UnitInertialParams";
            private const string GROUND_FORWARD_MOVE = "GroundForwardMove";
            private const string GROUND_BACKWARD_MOVE = "GroundBacwardMove";
            private const string GROUND_CROUCH_FORWARD_MOVE = "GroundCrouchForwardMove";
            private const string GROUND_CROUCH_BACKWARD_MOVE = "GroundCrouchBacwardMove";
            private const string AIR_FORWARD_MOVEA = "AirForwardMove";
            private const string AIR_BACKWARD_MOVE = "AirBackwardMove";
            private const string AIR_UP_FIRST_MOVE = "AirUpFirstMove";
            private const string AIR_UP_MOVE = "AirUpMove";
            private const string AIR_DOWN_MOVE = "AirDownMove";
            private const string JUMP_COUNT_MAX = "JumpCountMax";
            private const string JUMP_FIRST_HEIGHT = "JumpFirstHeight";
            private const string JUMP_HEIGHT = "JumpHeight";
            private const string HORIZONTAL_MOVE_MAX_ANGLE = "HorizontalMoveMaxAngle";

            public DirectionStateParams GroundForwardMove;
            public DirectionStateParams GroundBacwardMove;
            public DirectionStateParams GroundCrouchForwardMove;
            public DirectionStateParams GroundCrouchBacwardMove;
            public DirectionStateParams AirForwardMove;
            public DirectionStateParams AirBackwardMove;
            public DirectionStateParams AirUpFirstMove;
            public DirectionStateParams AirUpMove;
            public DirectionStateParams AirDownMove;

            public float JumpCountMax;
            public float JumpFirstHeight;
            public float JumpHeight;
            public float HorizontalMoveMaxAngle;

            private float mFirstJumpMaxTime;
            private float mOneJumpMaxTime;

            public void Init()
            {
                mFirstJumpMaxTime = JumpFirstHeight / AirUpFirstMove.SpeedMax;
                mOneJumpMaxTime = JumpHeight / AirUpMove.SpeedMax;
            }

            public float GetJumpTime(int jumpIndex)
            {
                return jumpIndex < 1 ? mFirstJumpMaxTime : mOneJumpMaxTime;
            }

            public DirectionStateParams GetMoveUpParams(int jumpIndex)
            {
                return jumpIndex < 1 ? AirUpFirstMove : AirUpMove;
            }

            public void ToXML(string path)
            {
                var storageFolder = new StorageFolder(UNIT_INERTIAL_PARAMS);

                var gfMove = new StorageFolder(GROUND_FORWARD_MOVE);
                gfMove.AddItem(GroundForwardMove.Serialize());

                var gbMove = new StorageFolder(GROUND_BACKWARD_MOVE);
                gbMove.AddItem(GroundBacwardMove.Serialize());

                var gcfMove = new StorageFolder(GROUND_CROUCH_FORWARD_MOVE);
                gcfMove.AddItem(GroundCrouchForwardMove.Serialize());

                var gcbMove = new StorageFolder(GROUND_CROUCH_BACKWARD_MOVE);
                gcbMove.AddItem(GroundCrouchBacwardMove.Serialize());

                var afMove = new StorageFolder(AIR_FORWARD_MOVEA);
                afMove.AddItem(AirForwardMove.Serialize());

                var abMove = new StorageFolder(AIR_BACKWARD_MOVE);
                abMove.AddItem(AirBackwardMove.Serialize());

                var aufMove = new StorageFolder(AIR_UP_FIRST_MOVE);
                aufMove.AddItem(AirUpFirstMove.Serialize());

                var auMove = new StorageFolder(AIR_UP_MOVE);
                auMove.AddItem(AirUpMove.Serialize());

                var adMove = new StorageFolder(AIR_DOWN_MOVE);
                adMove.AddItem(AirDownMove.Serialize());

                storageFolder.AddItem(gfMove);
                storageFolder.AddItem(gbMove);
                storageFolder.AddItem(gcfMove);
                storageFolder.AddItem(gcbMove);
                storageFolder.AddItem(afMove);
                storageFolder.AddItem(abMove);
                storageFolder.AddItem(aufMove);
                storageFolder.AddItem(auMove);
                storageFolder.AddItem(adMove);

                storageFolder.AddItem(new StorageFloat(JUMP_COUNT_MAX, JumpCountMax));
                storageFolder.AddItem(new StorageFloat(JUMP_FIRST_HEIGHT, JumpFirstHeight));
                storageFolder.AddItem(new StorageFloat(JUMP_HEIGHT, JumpHeight));
                storageFolder.AddItem(new StorageFloat(HORIZONTAL_MOVE_MAX_ANGLE, HorizontalMoveMaxAngle));

                CStorageSerializer.saveStorageToFile(path, storageFolder);
            }

            public static AgentInertionParams FromXDocument(XDocument xDocument)
            {
                StorageFolder from = new StorageFolder();
                CStorageSerializer.loadFromXmlDocument(xDocument, from);

                return From(from);
            }

            public static AgentInertionParams FromXML(string path)
            {
                StorageFolder from = new StorageFolder();
                CStorageSerializer.loadFromFile(path, from, false, true);

                return From(from);
            }

            private static AgentInertionParams From(StorageFolder from)
            {
                var groundForwardMove = DirectionStateParams.Deserialize(from.GetFolder(GROUND_FORWARD_MOVE));
                var groundBacwardMove = DirectionStateParams.Deserialize(from.GetFolder(GROUND_BACKWARD_MOVE));
                var groundCrouchForwardMove = DirectionStateParams.Deserialize(from.GetFolder(GROUND_CROUCH_FORWARD_MOVE));
                var groundCrouchBacwardMove = DirectionStateParams.Deserialize(from.GetFolder(GROUND_CROUCH_BACKWARD_MOVE));
                var airForwardMove = DirectionStateParams.Deserialize(from.GetFolder(AIR_FORWARD_MOVEA));
                var airBackwardMove = DirectionStateParams.Deserialize(from.GetFolder(AIR_BACKWARD_MOVE));
                var airUpFirstMove = DirectionStateParams.Deserialize(from.GetFolder(AIR_UP_FIRST_MOVE));
                var airUpMove = DirectionStateParams.Deserialize(from.GetFolder(AIR_UP_MOVE));
                var airDownMove = DirectionStateParams.Deserialize(from.GetFolder(AIR_DOWN_MOVE));

                var jumpCountMax = from.GetItemAsFloat(JUMP_COUNT_MAX);
                var JumpFirstHeight = from.GetItemAsFloat(JUMP_FIRST_HEIGHT);
                var jumpHeight = from.GetItemAsFloat(JUMP_HEIGHT);
                var horizontalMoveMaxAngle = from.GetItemAsFloat(HORIZONTAL_MOVE_MAX_ANGLE);

                var result = new AgentInertionParams()
                {
                    GroundForwardMove = groundForwardMove,
                    GroundBacwardMove = groundBacwardMove,
                    GroundCrouchForwardMove = groundCrouchForwardMove,
                    GroundCrouchBacwardMove = groundCrouchBacwardMove,
                    AirForwardMove = airForwardMove,
                    AirBackwardMove = airBackwardMove,
                    AirUpFirstMove = airUpFirstMove,
                    AirUpMove = airUpMove,
                    AirDownMove = airDownMove,
                    JumpCountMax = jumpCountMax,
                    JumpFirstHeight = JumpFirstHeight,
                    JumpHeight = jumpHeight,
                    HorizontalMoveMaxAngle = horizontalMoveMaxAngle
                };
                result.Init();

                return result;
            }

            public bool IsZero()
            {
                return GroundForwardMove.SpeedMax <= 0.0f &&
                    GroundBacwardMove.SpeedMax <= 0.0f &&
                    GroundCrouchForwardMove.SpeedMax <= 0.0f &&
                    GroundCrouchBacwardMove.SpeedMax <= 0.0f &&
                    AirForwardMove.SpeedMax <= 0.0f &&
                    AirBackwardMove.SpeedMax <= 0.0f &&
                    AirUpFirstMove.SpeedMax <= 0.0f &&
                    AirUpMove.SpeedMax <= 0.0f &&
                    AirDownMove.SpeedMax <= 0.0f;
            }
        }
    }
}
