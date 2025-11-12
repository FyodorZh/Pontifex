namespace Shared.CommonData.Plt
{
    public interface IWithGrades
    {
        short StartGrade { get; }

        ItemLevel[] Grades { get; }
    }
}
