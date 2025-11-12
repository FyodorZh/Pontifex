using Serializer.BinarySerializer;

namespace Shared.Protocol
{
    public enum MessageType : byte
    {
        // Сообщение об ошибке
        Error = 0,

        // Команда с сервера на начало загрузки данных боя
        PrepareForBattle = 1,

        // Команда с клиента о прогрессе подготовки старта боя
        InformServerAboutClientPreparingProgress = 2,
        // Команда с сервера о прогрессе подготовки старта боя всех участвующих игроков
        InformClientAboutClientPreparingProgress = 3,

        // Команда с клиента о готовности вступить в бой
        ReadyForBattle = 4,
        // Команда с сервера клиентам о старте боя
        StartBattle = 9,

        // Клиентская команда содержащая пользовательский ввод
        ClientLogicData = 6,
        // Серверная команда о состоянии игрового мира
        ServerLogicData = 7,

        // Информирование клиентов о завершении обя
        FinishBattle = 10,
        // Ответ клиентов о том что они проинформированы о завершении боя
        FinishBattleReceived = 11
    }

    public class MessageWrapper : IDataStruct
    {
        private byte mType;
        private IDataStruct mData;

        public MessageWrapper()
        {
        }

        public MessageWrapper(MessageType type, IDataStruct data)
        {
            mType = (byte)type;
            mData = data;
        }

        public MessageType Type
        {
            get { return (MessageType)mType; }
        }

        public IDataStruct Data
        {
            get { return mData; }
        }

        public bool Serialize(IBinarySerializer saver)
        {
            saver.Add(ref mType);
            saver.Add(ref mData);
            return true;
        }
    }
}
