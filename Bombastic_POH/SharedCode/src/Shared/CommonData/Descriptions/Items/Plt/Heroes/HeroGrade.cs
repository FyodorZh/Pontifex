//using Serializer.BinarySerializer;
//using Shared.CommonData.Attributes;
//
//namespace Shared.CommonData.Plt
//{
//    public class HeroGrade : IDataStruct
//    {
//        [EditorField(EditorFieldParameter.Unique)]
//        private short _grade;
//        [EditorField]
//        private int _time;
//        [EditorField]
//        private Price _price;
//        [EditorField]
//        private RpgParam[] _rpgParamsChange;
//        [EditorField]
//        private ItemWithCount[] _dropItems;
//        [EditorField]
//        private Requirement[] _requirements;
//
//        public HeroGrade()
//        {
//        }
//
//        public HeroGrade(short grade, int time, Price price, RpgParam[] rpgParamsChange, ItemWithCount[] dropItems, Requirement[] requirements)
//        {
//            _grade = grade;
//            _time = time;
//            _price = price;
//            _rpgParamsChange = rpgParamsChange;
//            _dropItems = dropItems;
//            _requirements = requirements;
//        }
//
//        public short Grade
//        {
//            get { return _grade; }
//        }
//
//        public System.TimeSpan Time
//        {
//            get { return System.TimeSpan.FromSeconds(_time); }
//        }
//
//        public Price Price
//        {
//            get { return _price; }
//        }
//
//        public RpgParam[] RpgParamsChange
//        {
//            get { return _rpgParamsChange; }
//        }
//
//        public ItemWithCount[] DropItems
//        {
//            get { return _dropItems; }
//        }
//
//        public Requirement[] Requirements
//        {
//            get { return _requirements; }
//        }
//
//        public bool Serialize(IBinarySerializer dst)
//        {
//            dst.Add(ref _grade);
//            dst.Add(ref _time);
//            dst.Add(ref _price);
//            dst.Add(ref _rpgParamsChange);
//            dst.Add(ref _dropItems);
//            dst.Add(ref _requirements);
//
//            return true;
//        }
//    }
//}
