using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using XRayBuilderGUI.DataSources.Amazon;
using XRayBuilderGUI.DataSources.Secondary;
using XRayBuilderGUI.Libraries.Http;
using XRayBuilderGUI.Libraries.Logging;
using XRayBuilderGUI.XRay.Logic;
using XRayBuilderGUI.XRay.Logic.Aliases;
using XRayBuilderGUI.XRay.Logic.Chapters;
using XRayBuilderGUI.XRay.Logic.Export;

namespace XRayBuilder.Test.XRay.Logic.Export
{
    public sealed class ExporterSqliteTests
    {
        private ILogger _logger;
        private IXRayExporter _xrayExporter;
        private Goodreads _goodreads;
        private IAliasesRepository _aliasesRepository;
        private IHttpClient _httpClient;
        private IAmazonClient _amazonClient;
        private IAmazonInfoParser _amazonInfoParser;
        private ChaptersService _chaptersService;
        private IXRayService _xrayService;

        [SetUp]
        public void Setup()
        {
            _logger = new Logger();
            _httpClient = new HttpClient(_logger);
            _amazonInfoParser = new AmazonInfoParser(_logger, _httpClient);
            _amazonClient = new AmazonClient(_httpClient, _amazonInfoParser, _logger);
            _xrayExporter = new XRayExporterSqlite(_logger);
            _goodreads = new Goodreads(_logger, _httpClient, _amazonClient);
            _aliasesRepository = new AliasesRepository(_logger);
            _chaptersService = new ChaptersService(_logger);
            _xrayService = new XRayService(new AliasesService(_logger), _logger);
        }

        [Test, TestCaseSource(typeof(TestData), nameof(TestData.Books))]
        public async Task XRayXMLSaveNewTest(Book book)
        {
            var xray = TestData.CreateXRayFromXML(book.xml, book.db, book.guid, book.asin, _goodreads, _logger, _chaptersService);
            await xray.CreateXray(null, CancellationToken.None);
            _xrayService.ExportAndDisplayTerms(xray, xray.AliasPath);
            _aliasesRepository.LoadAliasesForXRay(xray);
            xray.ExpandFromRawMl(new FileStream(book.rawml, FileMode.Open), null, null, CancellationToken.None, false, false);
            string filename = xray.XRayName();
            string outpath = Path.Combine(Environment.CurrentDirectory, "out", filename);
            _xrayExporter.Export(xray, outpath, null, CancellationToken.None);
        }
    }
}