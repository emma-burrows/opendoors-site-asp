//------------------------------------------------------------------------------
// <auto-generated>
//    This code was generated from a template.
//
//    Manual changes to this file may cause unexpected behavior in your application.
//    Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace OpenDoors.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class Chapter
    {
        public int ID { get; set; }
        public string Title { get; set; }
        public int AuthorID { get; set; }
        public string Text { get; set; }
        public Nullable<System.DateTime> Date { get; set; }
        public int StoryID { get; set; }
        public string Notes { get; set; }
        public Nullable<long> Position { get; set; }
        public string Url { get; set; }
    
        public virtual Story Story { get; set; }
    }
}
