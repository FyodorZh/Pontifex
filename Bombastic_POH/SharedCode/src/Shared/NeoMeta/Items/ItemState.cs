using System;
using System.Globalization;
using Shared.Utils;

namespace Shared.NeoMeta.Items
{
    public static class ItemStateExtensions
    {
        public static string AsString(this int self)
        {
            return self.ToString();

        }

        public static bool HasState(this int self, int state)
        {
            var selfByte = self;
            var stateByte = state;

            return ((selfByte & stateByte) == stateByte);
        }

        public static int AddState(this int self, int state)
        {
            if (!HasState(self, state))
            {
                var selfByte = self;
                var stateByte = state;

                var newState = selfByte | stateByte;

                return newState;
            }

            return self;
        }

        public static int RemoveState(this int self, int state)
        {
            if (HasState(self, state))
            {
                var selfByte = self;
                var stateByte = state;

                var newState = selfByte ^ stateByte;

                return newState;
            }

            return self;
        }
    }
}
