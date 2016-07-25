using OtwArchive;
using OtwArchive.Models.Request;
using OtwArchive.Models.Response;
using OpenDoors.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;
using System.Security.Policy;
using System.Web.Routing;
using System.Net;
using OtwArchive.Models.Request.Items;

namespace OpenDoors.Code
{
  public class ArchiveImporter
  {
    private Entities db;
    public ImportSettings ArchiveSettings { get; set; }
    private ArchiveClient archive;
    public ArchiveConfig config;

    public ArchiveImporter(Entities db)
    {
      this.db = db;

      NameValueCollection webConfig = (NameValueCollection)WebConfigurationManager.GetSection("ArchiveSettings");
      String key = webConfig["Key"];

      config = db.ArchiveConfigs.Where(c => c.Key == key).First();

      ArchiveSettings = new ImportSettings();
      ArchiveSettings.ArchiveHost         = webConfig["ArchiveHost"];
      ArchiveSettings.Token               = webConfig["Token"];
      ArchiveSettings.CollectionNames     = config.CollectionName;
      ArchiveSettings.Archivist           = config.Archivist;
      ArchiveSettings.SendClaimEmails     = config.SendEmail;
      ArchiveSettings.PostWithoutPreview  = config.PostWorks;
      ArchiveSettings.Encoding            = "UTF-8";

      archive = new ArchiveClient(ArchiveSettings);
    }

    // Custom methods
    // =============================================

    /// <summary>
    /// Import stories or bookmarks
    /// </summary>
    /// <param name="ids">Stories or bookmarks to import</param>
    /// <param name="requestContext">HTTP request context (to retrieve host)</param>
    /// <returns>Archive results with the final message, timing and list of story or bookmark messages</returns>
    public ArchiveResult importMany(int[] ids, RequestContext requestContext, 
                                    ImportSettings.ImportType type = ImportSettings.ImportType.Work)
    {
      List<String> messages = new List<string>();
      Dictionary<int, StoryResponse> workResults = new Dictionary<int, StoryResponse>();
      Dictionary<int, BookmarkResponse> bookmarkResults = new Dictionary<int, BookmarkResponse>();

      ImportRequest workimport = new ImportRequest();
      workimport.SendClaimEmails = ArchiveSettings.SendClaimEmails;
      workimport.PostWithoutPreview = ArchiveSettings.PostWithoutPreview;

      // Bookmarks
      if (type == ImportSettings.ImportType.Bookmark) {
        workimport.Bookmarks = new List<IRequestItem>();
        foreach (int id in ids)
        {
          Bookmark bookmark = db.Bookmarks.Find(id);
          if (!bookmark.DoNotImport)
            workimport.Bookmarks.Add(extBookmark(bookmark, requestContext));
        }
      }
      else
      {
      // Works
        workimport.Works = new List<IRequestItem>();
        foreach (int id in ids) 
        {
          Story story = db.Stories.Find(id);
          if (!story.DoNotImport)
            workimport.Works.Add(externalWork(story, requestContext));
        }
      }

      var stopwatch = Stopwatch.StartNew();
      ImportResponse result = archive.import(workimport, type);
      stopwatch.Stop();
      var elapsed = stopwatch.Elapsed;

      // Bundle up some general messages
      messages = new List<string> {
            String.Join("", result.Messages),
            String.Format("<p>Processing time: {0:00}m{1:00}s{2:00}.</p>", elapsed.Minutes, elapsed.Seconds, elapsed.Milliseconds)
          };

      // Stories
      if (result.Works != null && result.Works.Count() == ids.Count())
      {

        // Match the results to the ids (hopefully all in the same order...)
        workResults =
          ids.Zip(result.Works, (id, work) => new { id, work })
             .ToDictionary(
              item => item.id,
              item => new StoryResponse(item.work.Status, item.work.Url, item.work.OriginalUrl, item.work.Messages));

        foreach (KeyValuePair<int, StoryResponse> entry in workResults)
        {
          StoryResponse workResponse = entry.Value;
          Story story = db.Stories.Find(entry.Key);
          if (workResponse.Status == "created")
          {
            story.Imported = true;
            story.Ao3Url = workResponse.ArchiveUrl;
            config.NotImported = config.NotImported - 1;
            config.Imported = config.Imported + 1;
            db.Entry(story).State = EntityState.Modified;
          }
          else
          {
            messages.Add("error");
          }
        }
      }
      
      // bookmarks
      if (result.Bookmarks != null && result.Bookmarks.Count() == ids.Count())
      {
        bookmarkResults =
            ids.Zip(result.Bookmarks, (id, bookmark) => new { id, bookmark })
               .ToDictionary(
                item => item.id,
                item => new BookmarkResponse(item.bookmark.Status, item.bookmark.ArchiveUrl, item.bookmark.OriginalUrl, item.bookmark.Messages));

        foreach (KeyValuePair<int, BookmarkResponse> entry in bookmarkResults)
        {
          BookmarkResponse bookmarkResponse = entry.Value;
          Bookmark bookmark = db.Bookmarks.Find(entry.Key);
          if (bookmarkResponse.Status == "created")
          {
            bookmark.Imported = true;
            bookmark.Ao3Url = bookmarkResponse.ArchiveUrl;
            
            db.Entry(bookmark).State = EntityState.Modified;
          }
          else
          {
            messages.Add("error");
          }
        }
      }
      
      if (result.Works == null && result.Bookmarks == null)
      {
        messages.Add("error");
      }
      db.SaveChanges();

      return new ArchiveResult(messages, workResults, bookmarkResults);
    }

    /// <summary>
    /// Allow or disallow importing for specific stories
    /// </summary>
    /// <param name="ids">Story ids</param>
    /// <param name="doNotImport">True = do not import, False = allow import</param>
    /// <returns>ArchiveResult indicating</returns>
    public ArchiveResult importNone(int[] ids, bool doNotImport)
    {
      Dictionary<int, StoryResponse> storiesWithResponses = new Dictionary<int, StoryResponse>();
      var stories = db.Stories.Where(s => ids.Contains(s.ID));
      foreach (Story s in stories)
      {
        s.DoNotImport = doNotImport;
        db.Entry(s).State = EntityState.Modified;
        storiesWithResponses.Add(s.ID, new StoryResponse("updated", "", "", new List<string>() { "Updated " + s.Title + "." }));
      }
      db.SaveChanges();
      return new ArchiveResult(new List<String>() { "Updated import status" }, storiesWithResponses, null);
    }

    /// <summary>
    /// Get the URL of a set of stories that might have been imported
    /// </summary>
    /// <param name="ids"></param>
    /// <param name="requestContext"></param>
    /// <returns></returns>
    public ArchiveResult checkMany(int[] ids, RequestContext requestContext)
    {
      List<String> messages = new List<string>();
      Dictionary<int, WorksResponse> workResults = new Dictionary<int, WorksResponse>();
      Dictionary<int, StoryResponse> storiesWithResponses = new Dictionary<int,StoryResponse>();

      // Get the URL of the first chapter in the work
      List<String> originalUrls = new List<String>();
      foreach (int id in ids)
      {
        Story story = db.Stories.Find(id);
        originalUrls.Add(externalWork(story, requestContext).ChapterUrls.First().ToString());
      }

      // Get the URLs from the ArchiveImporter and mark works as imported or not
      CheckRequest req = new CheckRequest();
      req.OriginalUrls = originalUrls;
      CheckResponse result = archive.checkUrls(req);
      List<WorksResponse> worksResponses = result.worksResponses;
      if (worksResponses != null && worksResponses.Count() == ids.Count())
      {
        // Match the results to the ids
        workResults =
          ids.Zip(worksResponses, (id, work) => new { id, work })
             .ToDictionary(item => item.id, item => item.work);

        storiesWithResponses = workResults.Where(item => item.Value.OriginalUrl != null)
          .ToDictionary(
            item => item.Key,
            item => new StoryResponse(item.Value.Status, item.Value.WorkUrl, item.Value.OriginalUrl, new List<String>()));

        if (storiesWithResponses.Count() == 0)
        {
          messages.Add("None of the works have been imported.");
        }
        else
        {
          foreach (KeyValuePair<int, StoryResponse> entry in storiesWithResponses)
          {
            StoryResponse workResponse = entry.Value;
            Story story = db.Stories.Find(entry.Key);
            if (workResponse.Status == "ok" && !story.Imported)
            {
              config.Imported = config.Imported + 1;
              config.NotImported = config.NotImported - 1;
              story.Imported = true;
              story.Ao3Url = workResponse.ArchiveUrl;
              db.Entry(story).State = EntityState.Modified;
              workResponse.Messages.Add("Found work on the Archive and updated its status here.");
            }
            else if (workResponse.Status == "not_found" && story.Imported)
            {
              config.Imported = config.Imported - 1;
              config.NotImported = config.NotImported + 1;
              story.Imported = false;
              story.Ao3Url = "";
              db.Entry(story).State = EntityState.Modified;
              workResponse.Messages.Add("Did not find work on the Archive and updated its status here.");
            }
            else
            {
              workResponse.Messages.Add("Story status is correct.");
            }
          }
          db.SaveChanges();
        }
      }
      return new ArchiveResult(messages, storiesWithResponses, null);
    }

    // HELPERS
    private WorkImportRequest externalWork(Story story, RequestContext requestContext)
    {
      Author author = story.Author;

      WorkImportRequest external = new WorkImportRequest();
      external.ExternalAuthorName   = author.Name;
      external.ExternalAuthorEmail  = author.Email;
      external.Summary              = story.Summary;
      external.Fandoms              = story.Fandoms;
      external.Relationships        = story.Relationships;
      external.Characters           = story.Characters;
      external.Categories           = story.Categories;
      external.AdditionalTags       = story.Tags;
      external.Rating               = story.Rating;
      external.Title                = story.Title;
      if (!String.IsNullOrEmpty(story.Notes))
      {
        external.Notes              = story.Notes + "<br/><br/>";
      }
      external.Notes               += config.ODNote;
      external.Warnings             = story.Warnings;

      external.ChapterUrls = new List<Uri>();
      var chapters = story.Chapters.OrderBy(c => c.Position);
      if (chapters.Count() > 0)
      {
        // Stories with chapters
        foreach (Chapter c in chapters)
        {
          Uri url;
          if (String.IsNullOrEmpty(c.Url))
            url = new Uri(new UrlHelper(requestContext).Action("Details", "Chapter", new { id = c.ID }, Uri.UriSchemeHttp));
          else
            url = fullUrl(c.Url, requestContext);
          external.ChapterUrls.Add(url);
        }
      }
      else
      {
        // Stories with one single HTML chapter
        external.ChapterUrls.Add(fullUrl(story.Url, requestContext));
      }

      return external;
    }

    private BookmarkImportRequest extBookmark(Bookmark bookmark, RequestContext requestContext)
    {
      Author author = bookmark.Author;
      UrlHelper urlHelper = new UrlHelper(requestContext);

      BookmarkImportRequest external = new BookmarkImportRequest();
      external.Author = author.Name;
      external.Url = bookmark.Url;
      external.Summary = bookmark.Summary;
      external.FandomString = bookmark.Fandoms;
      external.RelationshipString = bookmark.Relationships;
      external.CharacterString = bookmark.Characters;
      external.CategoryString = bookmark.Categories;
      external.Title = bookmark.Title;
      external.RatingString = bookmark.Rating;
      external.CollectionNames = config.CollectionName;
      // work.WarningString = bookmark.Warnings;
      external.TagString = bookmark.Tags;
      external.Notes = bookmark.Notes;

      return external;
    }

    private Uri fullUrl(string url, RequestContext requestContext)
    {
      UrlHelper urlHelper = new UrlHelper(requestContext);
      var request = urlHelper.RequestContext.HttpContext.Request;
      var relativeUrl = urlHelper.Content("~/works/" + url);
      return new Uri(string.Format("{0}://{1}{2}", request.Url.Scheme, request.Url.Authority, relativeUrl));
    }
  }
}
