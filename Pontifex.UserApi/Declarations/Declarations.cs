using System;
using System.Collections.Generic;

namespace Pontifex.UserApi
{
    internal struct ReceivedMessage // TODO: remove this struct
    {
        public readonly IMemoryBufferHolder Buffer;
        public readonly IBinarySerializer Reader;

        public ReceivedMessage(IMemoryBufferHolder buffer)
        {
            Buffer = buffer;
            Reader = null;
        }

        public ReceivedMessage(IBinarySerializer reader)
        {
            Reader = reader;
            Buffer = null;
        }
    }

    internal interface IDataModelSender
    {
        bool IsServerMode { get; }
        SendResult Send(ushort declId, IMemoryBufferHolder data);
        SendResult Send<TDataStruct>(ushort declId, TDataStruct data) where TDataStruct : IDataStruct;
    }

    internal interface IDeclaration
    {
        void FillFactoryModels(HashSet<Type> types);
        void FillNonFactoryModels(HashSet<Type> types);

        void Prepare(ushort declarationId, IDataModelSender sender);
        bool OnReceived(ReceivedMessage data);

        void SetName(string name);
        string Name { get; }
        void Stop();
    }

    public abstract class Declaration : IDeclaration
    {
        private ushort mDeclarationId;
        private IDataModelSender mSender;

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

        protected abstract bool OnReceived(IBinarySerializer received);

        protected abstract bool OnReceived(IMemoryBufferHolder buffer);

        protected virtual void Prepare(bool isServerMode)
        {
        }

        public string Name
        {
            get { return mName; }
        }

        public abstract void Stop();

        protected SendResult Send<TDataStruct>(TDataStruct model)
            where TDataStruct : IDataStruct
        {
            return mSender.Send(mDeclarationId, model);
        }

        protected SendResult Send(IMemoryBufferHolder buffer)
        {
            return mSender.Send(mDeclarationId, buffer);
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