using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Search.Highlight;
using Lucene.Net.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitClone
{
    public static class LuceneSearch
    {
        private static string _luceneDir = Path.Combine(Environment.CurrentDirectory, "leucene_index");
        private static FSDirectory _directoryTemp;
        private static FSDirectory _directory
        {
            get
            {
                Console.WriteLine(_luceneDir);
                if (_directoryTemp == null)
                {
                    _directoryTemp = FSDirectory.Open(new DirectoryInfo(_luceneDir));
                }

                if (IndexWriter.IsLocked(_directoryTemp))
                {
                    IndexWriter.Unlock(_directoryTemp);
                }

                var lockFilePath = Path.Combine(_luceneDir, "write.lock");
                if (File.Exists(lockFilePath))
                {
                    File.Delete(lockFilePath);
                }
                return _directoryTemp;
            }
        }

        private static void _addToLuceneIndex(LuceneDataInfo info, IndexWriter writer)
        {
            var searchQuery = new TermQuery(new Term("FullName", info.FullName));
            writer.DeleteDocuments(searchQuery);

            var doc = new Document();

            doc.Add(new Field("Name", info.Name, Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("FullName", info.FullName, Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Content", info.Content, Field.Store.YES, Field.Index.ANALYZED));

            writer.AddDocument(doc);
        }

        public static void AddUpdateLuceneIndex(IEnumerable<LuceneDataInfo> data)
        {
            var analyzer = new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30);
            using (var writer = new IndexWriter(_directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                foreach (var item in data)
                {
                    _addToLuceneIndex(item, writer);
                }

                analyzer.Close();
            }
        }

        public static void AddUpdateLuceneIndex(LuceneDataInfo data)
        {
            var analyzer = new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30);
            using (var writer = new IndexWriter(_directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                _addToLuceneIndex(data, writer);

                analyzer.Close();
            }
        }

        public static void ClearLuceneIndexRecord(string recordName)
        {
            var analyzer = new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30);
            using (var writer = new IndexWriter(_directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                var searchQuery = new TermQuery(new Term("FullName", recordName));
                writer.DeleteDocuments(searchQuery);

                analyzer.Close();
            }
        }

        public static bool ClearLuceneIndex()
        {
            try
            {
                var analyzer = new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30);
                using (var writer = new IndexWriter(_directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
                {
                    writer.DeleteAll();

                    analyzer.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
            return true;
        }

        public static void Optimize()
        {
            var analyzer = new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30);
            using (var writer = new IndexWriter(_directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                analyzer.Close();
                writer.Optimize();
            }
        }

        private static LuceneDataInfo _mapLuceneDocumentToData(Document doc)
        {
            return new LuceneDataInfo(doc.Get("Name"), doc.Get("FullName"), doc.Get("Content"));
        }

        private static LuceneDataInfo _mapLuceneDocumentToDataScore(Document doc, float score, Highlighter highlighter, StandardAnalyzer analyzer)
        {
            return new LuceneDataInfo(getHighlight(highlighter, analyzer, doc.Get("Name")), doc.Get("FullName"), getHighlight(highlighter, analyzer, doc.Get("Content")), score);
        }

        private static IEnumerable<LuceneDataInfo> _mapLuceneToDataList(IEnumerable<Document> hits)
        {
            return hits.Select(_mapLuceneDocumentToData).ToList();
        }

        private static IEnumerable<LuceneDataInfo> _mapLuceneToDataList(IEnumerable<ScoreDoc> hits, IndexSearcher searcher, Highlighter highlighter, StandardAnalyzer analyzer)
        { 
            return hits.Select(hit => _mapLuceneDocumentToDataScore(searcher.Doc(hit.Doc), hit.Score, highlighter, analyzer)).ToList();
        }

        private static Query parseQuery(string searchQuery, QueryParser parser)
        {
            Query query;
            try
            {
                query = parser.Parse(searchQuery.Trim());
            }
            catch (ParseException)
            {
                query = parser.Parse(QueryParser.Escape(searchQuery.Trim()));
            }
            return query;
        }

        private static string getHighlight(Highlighter highlighter, StandardAnalyzer analyzer, string fieldContent)
        {
            TokenStream stream = analyzer.TokenStream("fieldContent", new StringReader(fieldContent));
            var highlighted = highlighter.GetBestFragments(stream, fieldContent, 5, "");
            return string.IsNullOrEmpty(highlighted) ? fieldContent : highlighted; 
        }

        private static IEnumerable<LuceneDataInfo> _search(string searchQuery)
        {
            if (string.IsNullOrEmpty(searchQuery.Replace("*", "").Replace("?", "")))
            {
                return new List<LuceneDataInfo>();
            }
            using (var searcher = new IndexSearcher(_directory, false))
            {
                var hits_limit = 1000;
                var analyzer = new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30);
                IFormatter formatter = new SimpleHTMLFormatter("<span style=\"font-weight:bold; background-color:yellow;\">", "</span>");
                MultiFieldQueryParser parser = new MultiFieldQueryParser(Lucene.Net.Util.Version.LUCENE_30, new[] { "Name", "FullName", "Content" }, analyzer);
                Query query = parseQuery(searchQuery, parser);
                QueryScorer scorer = new QueryScorer(query);
                SimpleSpanFragmenter fragmenter = new SimpleSpanFragmenter(scorer,1000);
                searcher.SetDefaultFieldSortScoring(true, true);
                var hits = searcher.Search(query, null, hits_limit, Sort.RELEVANCE).ScoreDocs;
                Highlighter highlighter = new Highlighter(formatter, scorer);
                highlighter.TextFragmenter = fragmenter;
                var results = _mapLuceneToDataList(hits, searcher, highlighter, analyzer);
                analyzer.Close();
                return results;
            }
        }

        public static IEnumerable<LuceneDataInfo> Search(string input)
        {
            if (string.IsNullOrEmpty(input))
                return new List<LuceneDataInfo>();

            var terms = input.Trim().Replace("-", " ").Split(' ').Where(x => !string.IsNullOrEmpty(x)).Select(x => x.Trim() + "*");
            input = string.Join(" ", terms);

            return _search(input);
        }

        public static IEnumerable<LuceneDataInfo> SearchDefault(string input)
        {
            return string.IsNullOrEmpty(input) ? new List<LuceneDataInfo>() : _search(input);
        }
    }

    class LuceneIndexData
    {
        public static LuceneDataInfo Get(string name)
        {
            return Data.SingleOrDefault(f => f.Name.Equals(name, StringComparison.InvariantCulture));
        }

        public static List<LuceneDataInfo> Data { get; set; } = new List<LuceneDataInfo>();
    }

    public class LuceneDataInfo
    {
        public LuceneDataInfo(string name, string fullName, string content)
        {
            Name = name;
            FullName = fullName;
            Content = content;
        }

        public LuceneDataInfo(string name, string fullName, string content, float score)
        {
            Name = name;
            FullName = fullName;
            Content = content;
            Score = score;
        }

        public string Name { get; set; }
        public string FullName { get; set; }
        public string Content { get; set; }
        public float Score { get; set; }
    }
}
