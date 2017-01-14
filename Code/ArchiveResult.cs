using OtwArchive.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OpenDoors.Code
{
  public class ArchiveResult
  {
    public ArchiveResult(List<String> messages, List<StoryResponse> workResponses, 
                         List<BookmarkResponse> bookmarkResponses)
    {
      Messages = messages;
      StoryResponses = workResponses;
      BookmarkResponses = bookmarkResponses;
    }
    public List<String> Messages { get; set; }
    public List<StoryResponse> StoryResponses { get; set; }
    public List<BookmarkResponse> BookmarkResponses { get; set; }

    public String ConcatAllMessages(String separator)
    {
      List<String> messages = Messages ?? new List<String>();
      List<String> allStoryMessages = StoryResponses == null ? new List<string>() : StoryResponses.Select(s => String.Concat(s.OriginalId, " - ", String.Join(", ", s.Messages))).ToList() ;
      List<String> allBookmarkMessages = BookmarkResponses == null ? new List<string>() : BookmarkResponses.Select(s => String.Concat(s.OriginalId, " - ", String.Join(", ", s.Messages))).ToList();
      return String.Join(separator,
        messages.Concat(allStoryMessages.Concat(allBookmarkMessages)));
    }
  }

  public interface IItemResponse
  {
    String Status { get; set; }
    String OriginalId { get; set; }
    String ArchiveUrl { get; set; }
    String OriginalUrl { get; set; }
    List<String> Messages { get; set; }
  }

  public class StoryResponse : IItemResponse
  {
    public StoryResponse(String status, String archiveUrl, String originalUrl, List<String> messages, String originalId)
    {
      OriginalId = originalId;
      Status = status;
      ArchiveUrl = archiveUrl;
      OriginalUrl = originalUrl;
      Messages = messages;
    }
    public String Status { get; set; }
    public String OriginalId { get; set; }
    public String ArchiveUrl { get; set; }
    public String OriginalUrl { get; set; }
    public List<String> Messages { get; set; }
  }

  public class BookmarkResponse : IItemResponse
  {
    public BookmarkResponse(String status, String archiveUrl, String originalUrl, List<String> messages, String originalId)
    {
      OriginalId = originalId;
      Status = status;
      ArchiveUrl = archiveUrl;
      OriginalUrl = originalUrl;
      Messages = messages;
    }
    public String Status { get; set; }
    public String OriginalId { get; set; }
    public String ArchiveUrl { get; set; }
    public String OriginalUrl { get; set; }
    public List<String> Messages { get; set; }
  }
}
