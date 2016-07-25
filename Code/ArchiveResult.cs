using OtwArchive.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OpenDoors.Code
{
  public class ArchiveResult
  {
    public ArchiveResult(List<String> messages, Dictionary<int, StoryResponse> workResponses, 
                         Dictionary<int, BookmarkResponse> bookmarkResponses)
    {
      Messages = messages;
      StoryResponses = workResponses;
      BookmarkResponses = bookmarkResponses;
    }
    public List<String> Messages { get; set; }
    public Dictionary<int, StoryResponse> StoryResponses { get; set; }
    public Dictionary<int, BookmarkResponse> BookmarkResponses { get; set; }
  }

  public class StoryResponse
  {
    public StoryResponse(String status, String archiveUrl, String originalUrl, List<String> messages, String originalRef = "0")
    {
      OriginalRef = originalRef;
      Status = status;
      ArchiveUrl = archiveUrl;
      OriginalUrl = originalUrl;
      Messages = messages;
    }
    public String OriginalRef { get; set; }
    public String Status { get; set; }
    public String ArchiveUrl { get; set; }
    public String OriginalUrl { get; set; }
    public List<String> Messages { get; set; }
  }

  public class BookmarkResponse
  {
    public BookmarkResponse(String status, String archiveUrl, String originalUrl, List<String> messages, String originalRef = "0")
    {
      OriginalRef = originalRef;
      Status = status;
      ArchiveUrl = archiveUrl;
      OriginalUrl = originalUrl;
      Messages = messages;
    }
    public String OriginalRef { get; set; }
    public String Status { get; set; }
    public String ArchiveUrl { get; set; }
    public String OriginalUrl { get; set; }
    public List<String> Messages { get; set; }
  }
}
