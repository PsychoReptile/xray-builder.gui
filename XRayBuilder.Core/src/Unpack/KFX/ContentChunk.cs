namespace XRayBuilder.Core.Unpack.KFX
{
    public class ContentChunk
    {
        public string Name { get; set; }
        public bool MatchZeroLen { get; set; }
        public string ContentName { get; set; }
        public string ContentText { get; set; }
        public int Pid { get; set; }
        public int Eid { get; set; }
        public int EidOffset { get; set; }
        public int Length { get; set; }
    }
}