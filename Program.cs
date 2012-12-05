using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FriendWords
{
    class Program
    {
        const int THREAD_COUNT = 8;
        const string START_WORD = "causes";
        const int TEST_FINAL_VALUE = 78482;
        const bool USE_WEBSITE_LIST = false;
        const string WEBSITE_URL = "https://github.com/causes/puzzles/raw/master/word_friends/word.list";
        const string PATH_TO_WORDLIST = "word_friends/word.list";

        static void Main(string[] args)
        {
            //trying enumerables - too much overhead, recreating list with each call, not easily updatable
            //trying lists of special class - still not as easy to update as a dictionary
            //trying dictionary
            //moving away from recursion - had stack overflow @ 24000 network
            //still taking ~60s to reach 2500 - trying search for possible matches vs list of all words
            //iterating through all possible 1-levdist from each friend has reduced time dramatically as opposed to 
            //  multiple scans against the large dictionary of all terms - checking in to github before breaking anything
            //  (~13s on beast computer, ~30s on slower one)
            //tried backgroundworkers - too slow
            //tried threadpool - not enough control
            //tried a dynamic list of threads and concurrent objects; w/ 4 threads, runs in ~17s on slow computer
            //removed ref'd count (was inaccurate), using dictionary for final numbers, w/ 8 threads ~14s on slow computer

            //Two words are friends if they have a Levenshtein distance (http://en.wikipedia.org/wiki/Levenshtein_distance) of 1.
            //That is, you can add, remove, or substitute exactly one letter in word X to create word Y.
            //A word’s social network consists of all of its friends, plus all of their friends, and all of their friends’ friends, and so on.
            //Write a program to tell us how big the social network for the word “causes” is, using this word list
            //(https://github.com/causes/puzzles/raw/master/word_friends/word.list).

            ConcurrentDictionary<string, bool> wordList;

            if (USE_WEBSITE_LIST)
                wordList = GetWordListFromWeb(WEBSITE_URL); //string - word; bool - whether it's in the network
            else
                wordList = GetWordListFromFile(PATH_TO_WORDLIST);

            ConcurrentStack<string> friends = new ConcurrentStack<string>();

            Stopwatch sw = Stopwatch.StartNew();
            if (wordList.Any()) //make sure we got some words back from the site
            {
                friends.Push(START_WORD);

                int threadCount = THREAD_COUNT;

                List<Thread> threads = new List<Thread>();
                for (int i = 0; i < threadCount; i++)
                {
                    threads.Add(new Thread(delegate()
                    {
                        while (friends.Count() > 0)
                        {
                            string popped = "";
                            if (friends.TryPop(out popped))
                            {
                                FindNextFriends(popped, ref friends, ref wordList);
                            }
                        }
                    }));
                }

                //fire off the threads
                foreach (Thread thread in threads)
                {
                    Console.WriteLine("starting thread");
                    thread.Start();
                }

                Console.WriteLine("waiting for threads to complete");

                //wait for everyone to come home
                foreach (Thread thread in threads)
                {
                    thread.Join();
                    Console.WriteLine("thread complete");
                }
            }
            else
            {
                Console.WriteLine("No words in word list");
            }
            sw.Stop();

            int count = wordList.Count(w => w.Value);

            Console.WriteLine("Found: {0} words in network; took {1}ms", count, sw.Elapsed.TotalMilliseconds);

            //78482 is the magic number
            Console.WriteLine("Testing Dictionary for target numer ({0}): {1}", TEST_FINAL_VALUE, (TEST_FINAL_VALUE == count).ToString());

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }

        private static void FindNextFriends(string startWord, ref ConcurrentStack<string> friends, ref ConcurrentDictionary<string, bool> wordList)
        {
            List<string> newFriends = new List<string>();
            Stack<string> distance1Levs = new Stack<string>();

            //grab next friend to grab their friends

            if (startWord.Length > 0)
            {
                for (int i = 0; i <= startWord.Length; i++) //for each position in the word
                {
                    for (char l = 'a'; l <= 'z'; l++) //for each possible letter
                    {
                        //add a letter
                        distance1Levs.Push(string.Format("{0}{1}{2}",
                            startWord.Substring(0, i), l, startWord.Substring(i, startWord.Length - i)));

                        //change a letter
                        if (i > 0)
                        {
                            distance1Levs.Push(string.Format("{0}{1}{2}",
                                startWord.Substring(0, i - 1), l, startWord.Substring(i, startWord.Length - i)));
                        }
                    }

                    //remove a letter
                    if (i > 0)
                    {
                        distance1Levs.Push(string.Format("{0}{1}",
                                    startWord.Substring(0, i - 1), startWord.Substring(i, startWord.Length - i)));
                    }
                }

                while (distance1Levs.Any())
                {
                    string item = distance1Levs.Pop();
                    if (wordList.ContainsKey(item))
                    {
                        bool val;
                        wordList.TryGetValue(item, out val);
                        if (!val) //double check we haven't already found this word's friends
                        {
                            friends.Push(item);
                            wordList.TryUpdate(item, true, false);
                        }
                    }

                }
            }
        }

        private static ConcurrentDictionary<string, bool> GetWordListFromWeb(string url)
        {
            string result = "";
            ConcurrentDictionary<string, bool> wordList = new ConcurrentDictionary<string, bool>();

            Console.WriteLine("Getting Word List from: {0}", url);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            try
            {
                HttpWebResponse webResponse = (HttpWebResponse)request.GetResponse();
                Stream responseStream = webResponse.GetResponseStream();
                StreamReader responseStreamReader = new StreamReader(responseStream);
                result = responseStreamReader.ReadToEnd();

                responseStream.Close();
                webResponse.Close();
            }
            catch (Exception ex)
            {
                result = "";
                Console.WriteLine("Error: {0}", ex.Message);
            }
            finally
            {
                foreach (string item in result.Split('\n'))
                    wordList.TryAdd(item, false);

                Console.WriteLine("Retrieved {0} Word(s)", wordList.Count());
            }

            return wordList;
        }

        private static ConcurrentDictionary<string, bool> GetWordListFromFile(string file)
        {
            ConcurrentDictionary<string, bool> wordList = new ConcurrentDictionary<string, bool>();

            Console.WriteLine("Reading Word List from: {0}", file);
            try
            {
                foreach (string item in File.ReadLines(file))
                    wordList.TryAdd(item, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }
            finally
            {
                Console.WriteLine("Retrieved {0} Word(s)", wordList.Count());
            }

            return wordList;
        }

    }
}