using System;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using java.util;
using java.io;
using edu.stanford.nlp.pipeline;
using Console = System.Console;
using File = System.IO.File;

namespace DataSearchEngine
{
    public static class Extensions
    {
        public static string GetIndentString(int indent)
        {
            string indentString = "";
            for (int i = 0; i < indent; i++)
            {
                indentString += "\t";
            }

            return indentString;
        }
        public static void ConsoleWriteLines(IEnumerable<string> words, int indent = 0)
        {
            string indentString = GetIndentString(indent);

            foreach (string word in words)
            {
                Console.WriteLine($"{indentString} {word}");
            }
        }

        public static void ConsoleWriteWords(IEnumerable<string> words, int indent = 0)
        {
            string indentString = GetIndentString(indent);
            
            foreach (string word in words)
            {
                Console.Write($"{indentString}{word} ");
            }
        }

        //Find result by matching all words in query given with database
        public static List<int> FindAll(Dictionary<string, HashSet<int>> database, List<string> searchQuery)
        {
            var searchResults = new Dictionary<int, int>();

            foreach (string word in searchQuery)
            {
                if (database.TryGetValue(word, out var documentIndexes) == false) continue;

                foreach (int documentIndex in documentIndexes)
                {
                    if (searchResults.ContainsKey(documentIndex))
                    {
                        searchResults[documentIndex]++;
                    }
                    else
                    {
                        searchResults.Add(documentIndex, 1);
                    }
                }
            }

            return (from result in searchResults where result.Value == searchQuery.Count select result.Key).ToList();
        }

        //Find result by matching any words in query given with database
        public static HashSet<string> FindAny(this Dictionary<string, List<string>> invertedIndexDatabase, IEnumerable<string> searchQuery)
        {
            var results = new HashSet<string>();

            foreach (string word in searchQuery)
            {
                if (invertedIndexDatabase.TryGetValue(word, out var documents))
                {
                    results.UnionWith(documents);
                }
            }

            return results;
        }
    }

    internal class EntryPoint
    {
        public static StanfordCoreNLP Pipeline;
        public static Dictionary<string, HashSet<int>> SingleValueDatabase;
        public static Dictionary<string, HashSet<int>> NGramDatabase;
        private static int _gramCount = 2;

        //For debugging
        public static List<string> DocumentNames;

        [STAThread]
        private static void Main(string[] args)
        {
            //-------------------------------------------------------------------------
            string modelsPath       = "";
            string documentPath     = "";
            
            if(args.Length == 0)
            {
                string projectDirectory = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;
                modelsPath       = Path.Combine(projectDirectory, "stanford-corenlp-3.7.0-models");
                documentPath     = Path.Combine(projectDirectory, "Database");
            }

            //Get models directory
            if (args.Length > 0)
            {
                modelsPath = args[0];
            }
            
            if (args.Length > 1)
            {
                documentPath = args[1];
            }
            
            if (args.Length > 2)
            {
                _gramCount = int.Parse(args[2]);
            }

            Console.WriteLine($"Initializing {nameof(StanfordCoreNLP)}...\n");

            // Annotation pipeline configuration
            var props = new Properties();
            props.setProperty("annotators", "tokenize, ssplit, pos, lemma,");
            props.setProperty("ner.useSUTime", "0");

            // We should change current directory, so StanfordCoreNLP could find all the model files automatically
            string curDir = Environment.CurrentDirectory;
            Directory.SetCurrentDirectory(modelsPath);
            Pipeline = new StanfordCoreNLP(props);
            Directory.SetCurrentDirectory(curDir);

            var files = Directory.GetFiles(documentPath);

            //Initialize database
            SingleValueDatabase = new Dictionary<string, HashSet<int>>(10000);
            NGramDatabase       = new Dictionary<string, HashSet<int>>(10000);
            DocumentNames       = new List<string>(files.Length);

            Console.WriteLine("\nBuilding Database...\n");

            //Populate database
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                DocumentNames.Add(Path.GetFileName(file));

                string content = File.ReadAllText(file);


                var deserializedDatabase = Annotate(content);

                var lemmas = FilterContent(deserializedDatabase.lemmas);
                AssignIndex(lemmas, i, ref SingleValueDatabase);
                AssignIndex(CreateNGrams(lemmas), i, ref NGramDatabase);
            }

            Console.WriteLine("\n---Database Built Successfully---\n");

            //Console Input --- search query
            Console.WriteLine("Input Your Search Query");
            string query = Console.ReadLine();
            if (query == null)
            {
                Console.WriteLine("Search query is empty, no results will be shown");
                Console.ReadLine();
                return;
            }
            Console.WriteLine();
            
            var deserializedSearchQuery = Annotate(query);

            //Find results
            var filteredSearchQuery = FilterContent(deserializedSearchQuery.lemmas);
            
            var searchQueryNGrams  = CreateNGrams(filteredSearchQuery);
            Console.WriteLine($"Searching with Grams={_gramCount}");
            ShowResults(FindAllAndMapResults(searchQueryNGrams, NGramDatabase), searchQueryNGrams);
            
            Console.WriteLine("\n<********************>\n");
            
            Console.WriteLine($"Searching by single values");
            ShowResults(FindAllAndMapResults(filteredSearchQuery, SingleValueDatabase), filteredSearchQuery);

            Console.ReadLine();
        }

        public static void ShowResults(List<string> results, List<string> searchQuery)
        {
            Console.WriteLine("Search Query");
            Extensions.ConsoleWriteLines(searchQuery, 1);

            if (results.Count == 0)
            {
                Console.WriteLine("\nNo Results Found");
            }
            else
            {
                Console.WriteLine("\nResults:");
                Console.WriteLine("<-------------------->");
                Extensions.ConsoleWriteLines(results);
                Console.WriteLine("<-------------------->");
            }
        }

        public static List<string> FindAllAndMapResults(List<string> searchQuery, Dictionary<string, HashSet<int>> database)
        {
            var foundResults       = Extensions.FindAll(database, searchQuery);
            var mappedResult = new List<string>(foundResults.Count);
            mappedResult.AddRange(foundResults.Select(result => DocumentNames[result]));
            return mappedResult;
        }
        
        public static AnnotationObject Annotate(string content)
        {
            // Annotation
            var annotation = new Annotation(content);
            Pipeline.annotate(annotation);

            // Result - Print
            using var stream = new ByteArrayOutputStream();

            Pipeline.jsonPrint(annotation, new PrintWriter(stream));

            //-----
            string serialized   = stream.toString().Replace("\n", "");
            var    deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<AnnotationObject>(serialized);

            //-----
            stream.close();

            return deserialized;
        }

        public static List<string> CreateNGrams(List<string> content)
        {
            var grams = new List<string>(content.Count);

            int indexCount = content.Count - (_gramCount - 1);

            for (int index = 0; index < indexCount; index++)
            {
                string gram = content[index];

                for (int j = 1; j < _gramCount; j++)
                {
                    gram = $"{gram} {content[index + j]}";
                }

                grams.Add(gram);
            }

            return grams;
        }

        //Create or assign indexes for words and documents
        private static void AssignIndex(List<string> content, int document, ref Dictionary<string, HashSet<int>> database)
        {
            var words = FilterContent(content);
            foreach (string word in words)
            {
                if (database.ContainsKey(word) == false)
                {
                    database.Add(word, new HashSet<int> {document});
                }
                else
                {
                    database[word].Add(document);
                }
            }
        }

        public static List<string> FilterContent(List<string> words)
        {
            var filteredContent = new List<string>(words.Count);
            foreach (string word in words)
            {
                if (char.IsLetterOrDigit(word[0]) == false) continue;
                string filteredWord = word.ToLower();
                filteredContent.Add(filteredWord);
            }

            return filteredContent;
        }
    }
}