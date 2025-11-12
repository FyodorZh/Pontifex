//using Serializer.BinarySerializer;
//
//namespace Shared.CommonData.Plt
//{
//    public class HeroLevelDescription : DescriptionBase
//    {
//        private Level _level;
//        private int _time;
//        private Price _price;
//        private Requirement[] _requirements;
//
//        public HeroLevelDescription()
//        {
//        }
//
//        public HeroLevelDescription(Level level, int time, Price price, Requirement[] requirements)
//        {
//            _level = level;
//            _time = time;
//            _price = price;
//            _requirements = requirements;
//        }
//
//        public Level Level
//        {
//            get { return _level; }
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
//        public Requirement[] Requirements
//        {
//            get { return _requirements; }
//        }
//
//        public bool Serialize(IBinarySerializer dst)
//        {
//            base.Serialize(dst);
//
//            dst.Add(ref _level);
//            dst.Add(ref _time);
//            dst.Add(ref _price);
//            dst.Add(ref _requirements);
//
//            return true;
//        }
//    }
//}
