using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using OpenDoors.Models;
using PagedList;
using OtwArchive;
using OpenDoors.Code;
using OtwArchive.Models.Request;

namespace OpenDoors.Controllers
{
  public class AuthorController : Controller
  {
    private Entities db = new Entities();
    private ArchiveImporter archive;
    private ArchiveConfig config;
    private List<String> letters;
    private Logging logger;

    public AuthorController()
    {
      logger = new Logging(Path.Combine(HttpRuntime.AppDomainAppPath + "\\Log" + DateTime.Today.ToString("-yyyy-MM-dd") + ".log"));
      archive = new ArchiveImporter(db);
      config = archive.config;
      letters = db.Authors
                  .Where(a => a.Stories.Count + a.Bookmarks.Count > 0)
                  .GroupBy(p => p.Name.Substring(0, 1))
                  .Select(x => x.Key.ToUpper())
                  .OrderBy(x => x)
                  .ToList();
    }
    //
    // GET: /Author/
    public ActionResult Index(string letter = "", int page = 1, int pageSize = 30)
    {
      letter = SetTempDataAndDefaultLetter(letter);

      if (letter == "_") letter = "\\_";
      String tableName = config.Key + "_authors";
      List<Author> authors = db.Authors
        .SqlQuery("SELECT * FROM " + tableName + " WHERE Name LIKE '" + letter + "%' ORDER BY Name")
        .ToList<Author>()
        .Where(a => a.Stories.Count + a.Bookmarks.Count > 0)
        .ToList<Author>();
          
      return View(authors.OrderBy(i => i.Name).ToPagedList(page, pageSize));
    }

    public ActionResult Import(int id, string letter = "", int page = 1, int pageSize = 30,
                               ImportSettings.ImportType type = ImportSettings.ImportType.Work)
    {
      letter = SetTempDataAndDefaultLetter(letter);
      
      var result = archive.importMany(new int[] { id }, this.Request.RequestContext, type);
      TempData["result"] = result;
      logger.Log(Response, string.Format("{0} [{1}]: Import {2} [{3}] - response: {4}",
        config.Name, Request.UserHostAddress, type.GetType(), id, String.Concat("\n", result.ConcatAllMessages("\n")))); 

      return RedirectToAction("Index", new { letter = letter, page = page, pageSize = pageSize });
    }

    public ActionResult ImportNone(int id, bool doNotImport, string letter = "", int page = 1, int pageSize = 30)
    {
      letter = SetTempDataAndDefaultLetter(letter);

      var author = db.Authors.Find(id);
      author.DoNotImport = doNotImport;

      db.Entry(author).State = EntityState.Modified;
      db.SaveChanges();

      int[] storyIds = author.Stories.Where(s => s.DoNotImport == !doNotImport).Select(s => s.ID).ToArray();
      int[] bookmarkIds = author.Bookmarks.Where(b => b.DoNotImport == !doNotImport).Select(b => b.ID).ToArray();

      ArchiveResult result = archive.importNone(storyIds, doNotImport, ImportSettings.ImportType.Work);
      ArchiveResult bookmarkResult = archive.importNone(bookmarkIds, doNotImport, ImportSettings.ImportType.Bookmark);
      result.BookmarkResponses = bookmarkResult.BookmarkResponses;
      
      TempData["result"] = result;
      logger.Log(Response, string.Format("{0} [{1}]: Set author '{2}' [{3}] DNI - response: {4}",
        config.Name, Request.UserHostAddress, author.Name, id, String.Concat("\n", result.ConcatAllMessages("\n")))); 

      return RedirectToAction("Index", new { letter = letter, page = page, pageSize = pageSize });
    }

    public ActionResult ImportAll(int id, string letter = "", int page = 1, int pageSize = 30,
                                  ImportSettings.ImportType type = ImportSettings.ImportType.Work)
    {
      letter = SetTempDataAndDefaultLetter(letter);

      int[] ids = new int[] {};
      Author author = db.Authors.Find(id);
      if (type == ImportSettings.ImportType.Work)
      {
        ids = author.Stories.Where(s => s.Imported == false).Select(s => s.ID).ToArray();
      }
      else if (type == ImportSettings.ImportType.Bookmark)
      {
        ids = author.Bookmarks.Where(b => b.Imported == false).Select(b => b.ID).ToArray();
      }

      ArchiveResult result = 
        new ArchiveResult(
          new List<String>() { String.Format("No response from Archive when importing all items for author '{0}'", author.Name) },
          null, null);
      if (ids.Count() == 0) {
        if (type == ImportSettings.ImportType.Bookmark) {
          result =
            new ArchiveResult(
                new List<String>() { String.Format("All bookmarks for author '{0}' [{1}] are already marked as imported", author.Name, author.ID) },
                null,
                new List<BookmarkResponse>());
        }
        else {
          result = 
            new ArchiveResult(
                new List<String>() { String.Format("All bookmarks for author '{0}' [{1}] are already marked as imported", author.Name, author.ID) }, 
                new List<StoryResponse>(),
                null);
        }
      } else {
         result = archive.importMany(ids, Request.RequestContext, type);
      }

      TempData["result"] = result;
      logger.Log(Response, string.Format("{0} [{1}]: Import all for author '{2}' [{3}] - response: {4}",
        config.Name, Request.UserHostAddress, author.Name, id, String.Concat("\n", result.ConcatAllMessages("\n")))); 

      return RedirectToAction("Index", new { letter = letter, page = page, pageSize = pageSize });
    }

    public ActionResult CheckAll(int id, string letter = "", int page = 1, int pageSize = 30)
    {
      letter = SetTempDataAndDefaultLetter(letter);

      Author author = db.Authors.Find(id);
      int[] ids = author.Stories.Select(s => s.ID).ToArray();
      var result = archive.checkMany(ids, Request.RequestContext);

      TempData["result"] = result;
      logger.Log(Response, string.Format("{0} [{1}]: Check all for author '{2}' [{3}] - response: {4}",
        config.Name, Request.UserHostAddress, author.Name, id, String.Concat("\n", result.ConcatAllMessages("\n"))));

      return RedirectToAction("Index", new { letter = letter, page = page, pageSize = pageSize });
    }

    protected override void Dispose(bool disposing)
    {
      db.Dispose();
      base.Dispose(disposing);
    }

    private String SetTempDataAndDefaultLetter(String letter)
    {
      TempData["config"] = config;
      TempData["letters"] = letters;

      if (String.IsNullOrEmpty(letter)) letter = letters.First();
      return letter;
    }
  }
}
