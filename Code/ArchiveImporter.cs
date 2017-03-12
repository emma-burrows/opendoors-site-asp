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
using System.IO;
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
    private Logging logger;

    public ArchiveImporter(Entities db)
    {
      this.db = db;

      logger = new Logging(Path.Combine(HttpRuntime.AppDomainAppPath + "\\Log" + DateTime.Today.ToString("-yyyy-MM-dd") + ".log"));

      NameValueCollection webConfig = (NameValueCollection)WebConfigurationManager.GetSection("ArchiveSettings");
      String key = webConfig["Key"];

      config = db.ArchiveConfigs.First(c => c.Key == key);

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
    /// <param name="author">author if there is one</param>
    /// <returns>Archive results with the final message, timing and list of story or bookmark messages</returns>
    public ArchiveResult ImportMany(int[] ids, RequestContext requestContext,
                                    String ipAddress, Author author = null,
                                    ImportSettings.ImportType type = ImportSettings.ImportType.Work)
    {
      List<String> messages = new List<string>();
      List<StoryResponse> workResults = new List<StoryResponse>();
      List<BookmarkResponse> bookmarkResults = new List<BookmarkResponse>();

      ImportRequest workimport = new ImportRequest();
      workimport.SendClaimEmails = ArchiveSettings.SendClaimEmails;
      workimport.PostWithoutPreview = ArchiveSettings.PostWithoutPreview;
      workimport.DetectTags = true;

      // Bookmarks
      if (type == ImportSettings.ImportType.Bookmark) {
        workimport.Bookmarks = new List<IRequestItem>();
        foreach (int id in ids)
        {
          Bookmark bookmark = db.Bookmarks.Find(id);
          if (!bookmark.DoNotImport && !bookmark.BrokenLink)
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
      if (author != null)
      {
        messages.Add(String.Format("author-{0}-messages", author.ID));
      } 
      messages.Add(String.Join("", result.Messages));
      messages.Add(String.Format("<p>Processing time: {0:00}m{1:00}s{2:00}.</p>", elapsed.Minutes, elapsed.Seconds, elapsed.Milliseconds));


      // Stories
      if (result.Works != null)
      {

        // Match the results to the ids (hopefully all in the same order...)
        workResults = result.Works.Select(item => new StoryResponse(item.Status, item.Url, item.OriginalUrl, item.Messages, item.OriginalId)).ToList();

        foreach (StoryResponse workResponse in workResults)
        {
          Story story = db.Stories.Find(Int32.Parse(workResponse.OriginalId));
          if (workResponse.Status == "created")
          {
            story.Imported = true;
            story.Ao3Url = workResponse.ArchiveUrl;
            story.ImportNotes += logger.Audit(ipAddress, string.Format("Imported to {0}", workResponse.ArchiveUrl));

            config.NotImported -= 1;
            config.Imported += 1;
            db.Entry(story).State = EntityState.Modified;
          }
          else if (workResponse.Messages.Exists(m => m.StartsWith("A work has already been imported")))
          {
            story.Imported = true;
            story.ImportNotes += logger.Audit(ipAddress, "Import requested but was already imported");

            // story.Ao3Url = workResponse.ArchiveUrl; // TODO: Change Archive to return URL

            db.Entry(story).State = EntityState.Modified;
          }
          else
          {
            messages.Add("error");
          }
        }
      }
      
      // bookmarks
      if (result.Bookmarks != null)
      {
        bookmarkResults = result.Bookmarks
          .Select(item => new BookmarkResponse(item.Status, item.ArchiveUrl, item.OriginalUrl, item.Messages, item.OriginalId))
          .ToList();

        foreach (BookmarkResponse bookmarkResponse in bookmarkResults)
        {
          Bookmark bookmark = db.Bookmarks.Find(Int32.Parse(bookmarkResponse.OriginalId));
          if (bookmarkResponse.Status == "created")
          {
            bookmark.Imported = true;
            bookmark.Ao3Url = bookmarkResponse.ArchiveUrl;
            bookmark.ImportNotes += logger.Audit(ipAddress, string.Format("Imported to {0}", bookmarkResponse.ArchiveUrl));
            
            db.Entry(bookmark).State = EntityState.Modified;
          }
          else if (bookmarkResponse.Messages.Exists(m => m.StartsWith("There is already a bookmark for this archivist")))
          {
            bookmark.Imported = true;
            bookmark.ImportNotes += logger.Audit(ipAddress, "Imported requested but was already imported");

            // bookmark.Ao3Url = bookmarkResponse.ArchiveUrl; // TODO: Change Archive to return URL
            
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
    /// Allow or disallow importing for specific items
    /// </summary>
    /// <param name="ids">Item ids</param>
    /// <param name="doNotImport">True = do not import, False = allow import</param>
    /// <param name="type">ImportSettings.ImportType</param>
    /// <returns>ArchiveResult indicating</returns>
    public ArchiveResult ImportNone(int[] ids, bool doNotImport, String ipAddress,
                                    ImportSettings.ImportType type = ImportSettings.ImportType.Work)
    {
      List<StoryResponse> storiesWithResponses = null;
      List<BookmarkResponse> bookmarksWithResponses = null;
      if (type == ImportSettings.ImportType.Work)
      {
        storiesWithResponses = new List<StoryResponse>();
        var stories = db.Stories.Where(s => ids.Contains(s.ID));
        foreach (Story s in stories)
        {
          s.DoNotImport = doNotImport;
          s.ImportNotes += logger.Audit(ipAddress, string.Format("Set to {0}", doNotImport ? "do NOT import" : "allow importing"));

          db.Entry(s).State = EntityState.Modified;
          storiesWithResponses.Add(new StoryResponse("updated", "", "", new List<string>() {"Updated " + s.Title + "."},
            s.ID.ToString()));
        }
      }
      if (type == ImportSettings.ImportType.Bookmark)
      {
        bookmarksWithResponses = new List<BookmarkResponse>();
        var bookmarks = db.Bookmarks.Where(b => ids.Contains(b.ID));
        foreach (Bookmark b in bookmarks)
        {
          b.DoNotImport = doNotImport;
          b.ImportNotes += logger.Audit(ipAddress, string.Format("Set to {0}", doNotImport ? "do NOT import" : "allow importing"));

          db.Entry(b).State = EntityState.Modified;
          bookmarksWithResponses.Add(new BookmarkResponse("updated", "", "", new List<string>() { "Updated " + b.Title + "." },
            b.ID.ToString()));
        }
      }
      db.SaveChanges();
      return new ArchiveResult(new List<String>() { "Updated import status" }, storiesWithResponses, bookmarksWithResponses);
    }

    /// <summary>
    /// Get the URL of a set of stories that might have been imported
    /// </summary>
    /// <param name="ids"></param>
    /// <param name="requestContext"></param>
    /// <returns></returns>
    public ArchiveResult CheckMany(int[] ids, RequestContext requestContext, String ipAddress)
    {
      List<String> messages = new List<string>();
      List<WorksResponse> workResults = new List<WorksResponse>();
      List<StoryResponse> storiesWithResponses = new List<StoryResponse>();

      // Get the URL of the first chapter in the work
      List<CheckRequestUrls> originalUrls = new List<CheckRequestUrls>();
      if (ids != null && ids.Length > 0) {
        foreach (int id in ids)
        {
          Story story = db.Stories.Find(id);
          originalUrls.Add(new CheckRequestUrls(id.ToString(), externalWork(story, requestContext).ChapterUrls.First().ToString()));
        }

        // Get the URLs from the ArchiveImporter and mark works as imported or not
        CheckRequest req = new CheckRequest();
        req.OriginalUrls = originalUrls;
        CheckResponse result = archive.checkUrls(req);
        List<WorksResponse> worksResponses = result.worksResponses;
        if (worksResponses != null && worksResponses.Count() == ids.Count())
        {
          // Match the results to the ids
          workResults = worksResponses;

          storiesWithResponses = workResults.Where(item => item.OriginalUrl != null)
            .Select(
              item =>
                new StoryResponse(item.Status, item.WorkUrl, item.OriginalUrl, new List<String>(), item.OriginalId))
            .ToList();

          if (storiesWithResponses.Any())
          {
            foreach (StoryResponse workResponse in storiesWithResponses)
            {
              Story story = db.Stories.Find(Int32.Parse(workResponse.OriginalId));
              if (workResponse.Status == "ok" && !story.Imported)
              {
                config.Imported = config.Imported + 1;
                config.NotImported = config.NotImported - 1;
                story.Imported = true;
                story.Ao3Url = workResponse.ArchiveUrl;
                story.ImportNotes += logger.Audit(ipAddress, string.Format("Checked and found at {0}", workResponse.ArchiveUrl));
                db.Entry(story).State = EntityState.Modified;
                workResponse.Messages.Add("Found work on the Archive and updated its status here.");
              }
              else if (workResponse.Status == "not_found" && story.Imported)
              {
                config.Imported = config.Imported - 1;
                config.NotImported = config.NotImported + 1;
                story.Imported = false;
                story.Ao3Url = "";
                story.ImportNotes += logger.Audit(ipAddress, "Checked and not found");
                db.Entry(story).State = EntityState.Modified;
                workResponse.Messages.Add("Did not find work on the Archive and updated its status here.");
              }
              else
              {
                if (story.Ao3Url != workResponse.ArchiveUrl)
                {
                  story.Ao3Url = workResponse.ArchiveUrl;
                  story.ImportNotes += logger.Audit(ipAddress, string.Format("Checked and Ao3Url was updated to {0}", workResponse.ArchiveUrl));
                  db.Entry(story).State = EntityState.Modified;
                }
                workResponse.Messages.Add("Story status is correct.");
              }
            }
            db.SaveChanges();
          }
          else
          {
            messages.Add("None of the works were checked.");
          }
        }
      }
      else
      {
        messages.Add("No works were selected for checking.");
      }
      return new ArchiveResult(messages, storiesWithResponses, null);
    }

    // HELPERS
    private WorkImportRequest externalWork(Story story, RequestContext requestContext)
    {
      Author author = story.Author;
      Author coauthor = story.CoAuthor;

      WorkImportRequest external = new WorkImportRequest();
      external.ExternalAuthorName   = author.Name;
      external.ExternalAuthorEmail  = author.Email;
      if (story.CoAuthor != null)
      {
        external.ExternalCoauthorName = coauthor.Name;
        external.ExternalCoauthorEmail = coauthor.Email;
      }
      external.Id                   = story.ID.ToString();
      external.Summary              = story.Summary;
      external.Fandoms              = story.Fandoms;
      external.Relationships        = story.Relationships;
      external.Characters           = story.Characters;
      external.Categories           = story.Categories;
      external.AdditionalTags       = story.Tags;
      external.Rating               = story.Rating;
      external.Title                = story.Title;
      external.Notes                = config.ODNote;
      if (!String.IsNullOrEmpty(story.Notes))
      {
        external.Notes              += "<br/><br/><b>Author's notes:</b> " + story.Notes;
      }
      external.Warnings             = story.Warnings;

      external.ChapterUrls = new List<Uri>();
      var chapters = story.Chapters.OrderBy(c => c.Position);
      if (chapters.Any())
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
      external.Id = bookmark.ID.ToString();
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
      external.Notes = config.BookmarksNote;
      if (!String.IsNullOrEmpty(bookmark.Notes))
      {
        external.Notes += "\n\n" + bookmark.Notes;
      }

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
