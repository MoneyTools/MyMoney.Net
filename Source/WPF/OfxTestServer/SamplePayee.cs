namespace OfxTestServer
{
    public class SamplePayee
    {
        public SamplePayee(string name, double min, double max)
        {
            this.Name = name;
            this.Min = min;
            this.Max = max;
        }

        public string Name { get; set; }

        public double Min { get; set; }

        public double Max { get; set; }
    }
}
