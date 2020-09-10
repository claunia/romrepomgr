using System.ComponentModel.DataAnnotations;

namespace RomRepoMgr.Database.Models
{
    public class RomSetStat
    {
        public         long   TotalMachines      { get; set; }
        public         long   CompleteMachines   { get; set; }
        public         long   IncompleteMachines { get; set; }
        public         long   TotalRoms          { get; set; }
        public         long   HaveRoms           { get; set; }
        public         long   MissRoms           { get; set; }
        public virtual RomSet RomSet             { get; set; }
        [Key]
        public long RomSetId { get; set; }
    }
}