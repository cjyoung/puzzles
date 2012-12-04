using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FriendWords
{
    class Program
    {
        static void Main(string[] args)
        {
            //trying enumerables - too much overhead, recreating list with each call, not easily updatable
            //trying lists of special class - still not as easy to update as a dictionary
            //trying dictionary
            //moving away from recursion - had stack overflow @ 24000 network
            //still taking ~60s to reach 2500 - trying search for possible matches vs list of all words
            //iterating through all possible 1-levdist from each friend has reduced time dramatically as opposed to 
            //  multiple scans against the large dictionary of all terms - checking in to github before breaking anything
            //  (~13s on beast computer, ~23 on slower one)

            //Two words are friends if they have a Levenshtein distance (http://en.wikipedia.org/wiki/Levenshtein_distance) of 1.
            //That is, you can add, remove, or substitute exactly one letter in word X to create word Y.
            //A word’s social network consists of all of its friends, plus all of their friends, and all of their friends’ friends, and so on.
            //Write a program to tell us how big the social network for the word “causes” is, using this word list
            //(https://github.com/causes/puzzles/raw/master/word_friends/word.list).

            string url = "https://github.com/causes/puzzles/raw/master/word_friends/word.list";
            string startWord = "causes";

            Dictionary<string, bool> wordList = GetWordList(url); //string - word; bool - whether it's in the network

            int count = 0;
            Stack<string> friends = new Stack<string>();
            List<string> newFriends = new List<string>();
            Stack<string> distance1Levs = new Stack<string>();

            Stopwatch sw = Stopwatch.StartNew();
            if (wordList.Any()) //make sure we got some words back from the site
            {
                friends.Push(startWord); //seed with first word
                while (friends.Any())
                {
                    startWord = friends.Pop(); //grab next friend to grab their friends

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
                            if (!wordList[item]) //double check we haven't already found this word's friends
                            {
                                count++;
                                if (count % 1000 == 0)
                                    Console.WriteLine("Found: {0} words in network", count);
                                friends.Push(item);
                                wordList[item] = true;
                            }
                        }
                    }
                }

            }
            else
            {
                Console.WriteLine("No words in word list");
            }
            sw.Stop();

            Console.WriteLine("Found: {0} words in network; took {1}ms", count, sw.Elapsed.TotalMilliseconds);

            int countFromDictionary = wordList.Count(w => w.Value);
            Console.WriteLine("Network size from Dictionary: {0}", count);

            Console.WriteLine("Count and Dictionary sizes match: {0}", (count == countFromDictionary).ToString());

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }

        private static Dictionary<string, bool> GetWordList(string url)
        {
            string result = "";
            Dictionary<string, bool> wordList = new Dictionary<string, bool>();

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
                //use dictionary, quicker to look up, no dupes, value determines if part of network
                wordList = result.Split('\n').Select(n => n).ToDictionary(k => k, v => false);
                Console.WriteLine("Retrieved {0} Word(s)", wordList.Count());
            }

            return wordList;
        }
    }
}