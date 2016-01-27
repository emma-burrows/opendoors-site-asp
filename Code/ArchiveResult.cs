using OtwArchive.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OpenDoors.Code
{
  public class ArchiveResult
  {
    public ArchiveResult(List<String> messages, Dictionary<int, StoryResponse> workResponses)
    {
      Messages = messages;
      StoryResponses = workResponses;
    }
    public List<String> Messages { get; set; }
    public Dictionary<int, StoryResponse> StoryResponses { get; set; }
  }

  public class StoryResponse
  {
    public StoryResponse(String status, String archiveUrl, String originalUrl, List<String> messages)
    {
      Status = status;
      ArchiveUrl = archiveUrl;
      OriginalUrl = originalUrl;
      Messages = messages;
    }
    public String Status { get; set; }
    public String ArchiveUrl { get; set; }
    public String OriginalUrl { get; set; }
    public List<String> Messages { get; set; }
  }
}
