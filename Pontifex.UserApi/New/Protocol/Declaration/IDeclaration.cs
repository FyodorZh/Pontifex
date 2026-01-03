using System;
using System.Collections.Generic;

namespace Pontifex.UserApi
{
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
}