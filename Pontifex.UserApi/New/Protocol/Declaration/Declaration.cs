using System;
using System.Collections.Generic;
using Archivarius;
using Pontifex.Utils;

namespace Pontifex.UserApi
{
    public abstract class Declaration : IDeclaration
    {
        private ushort mDeclarationId;
        private IDataModelSender? mSender;

        private string mName = "";

        /// <summary>
        /// Заполняется теми модельками, которые должны быть зарегистрированны в фабрике
        /// </summary>
        /// <param name="types"></param>
        protected abstract void FillFactoryModels(HashSet<Type> types);

        /// <summary>
        /// Заполняется модельками, которые не нужны фабрике, но влияют на протокльный хэш
        /// </summary>
        /// <param name="types"></param>
        protected abstract void FillNonFactoryModels(HashSet<Type> types);

        protected abstract bool OnReceived(ISerializer received);

        protected abstract bool OnReceived(UnionDataList buffer);

        protected virtual void Prepare(bool isServerMode)
        {
        }

        public string Name => mName;

        public abstract void Stop();

        protected SendResult Send<TDataStruct>(TDataStruct model)
            where TDataStruct : IDataStruct
        {
            return mSender?.Send(mDeclarationId, model) ?? SendResult.NotConnected;
        }

        protected SendResult Send(UnionDataList buffer)
        {
            return mSender?.Send(mDeclarationId, buffer) ?? SendResult.NotConnected;
        }

        void IDeclaration.FillFactoryModels(HashSet<Type> types)
        {
            FillFactoryModels(types);
        }

        void IDeclaration.FillNonFactoryModels(HashSet<Type> types)
        {
            FillNonFactoryModels(types);
        }

        void IDeclaration.Prepare(ushort declarationId, IDataModelSender sender)
        {
            mDeclarationId = declarationId;
            mSender = sender;
            Prepare(sender.IsServerMode);
        }

        bool IDeclaration.OnReceived(ReceivedMessage data)
        {
            if (data.Reader != null)
            {
                return OnReceived(data.Reader);
            }

            if (data.Buffer != null)
            {
                return OnReceived(data.Buffer);
            }

            return false;
        }

        void IDeclaration.SetName(string name)
        {
            mName = name;
        }
    }
}