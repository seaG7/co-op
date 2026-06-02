namespace MicroWorldNS
{
    /// <summary> Forces the system to select one of the objects of the given type. </summary>
    public interface IExclusive
    {
        public float Chance { get; }
        public string ExclusiveGroup { get; }
    }
}
