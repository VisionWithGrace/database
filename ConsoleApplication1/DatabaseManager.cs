using System;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using MongoDB.Driver.Builders;

using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver.Wrappers;

using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using System.Text;

/**
 * TODO:
 * If objects other than strings need to be stored, create necessary methods
 * and serialization map: http://docs.mongodb.org/ecosystem/tutorial/serialize-documents-with-the-csharp-driver/
 * 
 * Failure functions when get/insert fails
 * 
 * Image storing/retrieving
 * Some useful links:
 * http://docs.mongodb.org/manual/core/gridfs/
 * http://stackoverflow.com/questions/4988436/mongodb-gridfs-with-c-how-to-store-files-such-as-images
 * 
**/



/**gui interface:
 * saveSelectedImage(Image image, string identifyingInfo)
 * 
 * editObject(key, value, string newInfo) edit object where key=value
 * 
 * getUnidentifiedObjects()
 * 
 * getRecentObjects() last objects added
 * 
 * getLikelyObjects(key, value) likely objects based on some parameter (time, tag, etc)
 *                              heuristic logic goes here 
 * 
 * 
 * 
 */

namespace DatabaseModule
{
    
    public class DatabaseManager
    {
        private MongoServer server;
        private MongoDatabase objectsDatabase;
        private string collectionName;
        // private MongoCollection recognizedObjects;
        public DatabaseManager()
        {
            collectionName = "SelectedObjects";
            Connect();
        }

        private void Connect()
        {

            const string remoteConnectionString = "mongodb://67.194.7.120";
            MongoClient client = new MongoClient(remoteConnectionString);
            server = client.GetServer();
            objectsDatabase = server.GetDatabase("choices_db");

        }
        private QueryDocument queryFromString(string queryString)
        {
            BsonDocument query = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonDocument>(queryString);
            return new QueryDocument(query);
        }
        private static Random random = new Random((int)DateTime.Now.Ticks);
        private string RandomString(int size)
        {
            StringBuilder builder = new StringBuilder();
            char ch;
            for (int i = 0; i < size; i++)
            {
                ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65)));                 
                builder.Append(ch);
            }

            return builder.ToString();
        }

        //inserts a bson document into specified collection
        //BSON is a key-value dicitonary similar to JSON
        public void Insert(string collectionName, BsonDocument documentToAdd)
        {

            var objectsCollection = objectsDatabase.GetCollection(collectionName);
            objectsCollection.Insert(documentToAdd);

        }
        //inserts a bson document into specified collection
        //documentToAdd is a JSON-formatted key-value dicitonary
        public void Insert(string collectionName, string documentToAdd)
        {
            this.Insert(collectionName, BsonDocument.Parse(documentToAdd));
        }
        //inserts an image with the given metadata into specified collection
        //details should be a JSON-formatted string (dictionary of key-value pairs)
        public void InsertImage(string collectionName, Image image, string filename, Dictionary<string, object> info)
        {
            using (var fs = new System.IO.MemoryStream())
            {
                image.Save(fs, ImageFormat.Jpeg);
                fs.Position = 0;
                var gridFsInfo = objectsDatabase.GridFS.Upload(fs, filename);
                var fileId = gridFsInfo.Id;

                var collection = objectsDatabase.GetCollection(collectionName);
                BsonDocument bson;
                try
                {
                    bson = new BsonDocument(info);
                }
                catch(Exception e)
                {
                    bson = BsonDocument.Parse("{}");
                }
          
                bson.Add(new BsonElement("filename", filename));
                this.Insert(collectionName, bson);
            }

        }
        
        public Image GetImage(string collectionName, string key, string value)
        {
            var cursor = this.Get(collectionName, key, value);
            foreach (BsonDocument document in cursor)
            {
                Image image = GetImage(document);
                if (image != null)
                {
                    return image;
                }

            }
            return null;

        }
        //get image associated with document
        public Image GetImage(BsonDocument document)
        {
            var filename = document["filename"];
            System.Console.Write(filename);
            var file = objectsDatabase.GridFS.FindOne(Query.EQ("filename", filename));

            if (file == null)
            {
                return null;
            }
            System.Console.Write("Found file:");
            System.Console.Write(file);

            using (var stream = file.OpenRead())
            {
                var image = Image.FromStream(stream, true);
                return image;

            }
        }
        
        /*
         * Adds an object recognized by the CV side to the database
         * Right now relying on an integer ID. Will need to be hooked up to the 
         */
        public void addRecgonizedObject(RecognizedObject recObj)
        {
            BsonDocument doc = BsonDocument.Create(recObj);
            this.Insert(collectionName, doc);
        }

        /*
            edit object where identifyingKey= identifyingValue (i.e., “location” = “home”, or “id” = 7).
            Leaning towards some stack overflow-ish tagging system where Grace’s aid is able to add objects to some general tag (home, school, but in addition things like food, toys)
        */
        void modifyObject(string identifyingKey, string identifyingValue, string fieldName, BsonValue changedValue)
        {
            MongoCursor objectsToEdit = Get(collectionName, Query.EQ(identifyingKey, identifyingValue));
            foreach (BsonDocument doc in objectsToEdit)
            {
                doc.Set(fieldName, changedValue);
                objectsDatabase.GetCollection(collectionName).Save(doc); //Save document back into the collection
            }
        }

        /*
         * Adds the list of tags to the tags array accessed through the key "tags"
         * Careful. Will currently allow adding the same tag multiple times
         * Reference: http://stackoverflow.com/questions/11553481/updating-elements-inside-of-an-array-within-a-bsondocument
         *            http://stackoverflow.com/questions/6260936/adding-bson-array-to-bsondocument-in-mongodb
         */
        void addTags(string identifyingKey, string identifyingValue, List<string> tagsToAdd)
        {
            MongoCursor objectsToTag = Get(collectionName, Query.EQ(identifyingKey, identifyingValue));
            foreach (BsonDocument doc in objectsToTag)
            {
                
                if (!doc.Contains("tags")) // i.e. it wasn't found as an array yet
                {
                    System.Console.WriteLine("Creating a tag array with an object for the first time.");
                    BsonArray tmp = new BsonArray { };
                    doc.Add("tags", tmp);
                }
                var tagArray = doc["tags"].AsBsonArray;
                foreach (string tagToAdd in tagsToAdd) //Iterates through strings to add, appending them to array
                {
                    tagArray.Add(tagToAdd);
                }
                objectsDatabase.GetCollection(collectionName).Save(doc);
            }
        }

        /*
            Clears tags specified by oldTags.
            Can add functionality to clearAllTags if it's needed
        */
        void clearTags(string identifyingKey, string identifyingValue, List<string> tagsToRemove)
        {
            MongoCursor docsToClearTags = Get(collectionName, Query.EQ(identifyingKey, identifyingValue));
            foreach (BsonDocument doc in docsToClearTags)
            {
                if (doc.Contains("tags")) //Tags array does exist
                {
                    BsonArray tagArray = doc["tags"].AsBsonArray;
                    int index;
                    foreach (string tagToRemove in tagsToRemove)
                    {
                        index = tagArray.IndexOf(tagToRemove);
                        if (index >= 0) //IndexOf returns -1 if not found
                        {
                            tagArray.RemoveAt(index);
                        }
                    }
                    objectsDatabase.GetCollection(collectionName).Save(doc);
                }
            }
        }

        /*
         * Returns a list of tags associated with an object that matches the key value pair passed in
         * Currently requiring the key value pair to return one unique element, will return null otherwise
         */
        List<string> getTags(string identifyingKey, string identifyingValue)
        {
            MongoCursor matchedObjects = Get(collectionName, Query.EQ(identifyingKey, identifyingValue));
            List<string> foundTags = new List<string>();
            if (matchedObjects.Size() == 1) //Not sure what we want to do about this. Do we want to return a list of tags for multiple objects?
            {
                foreach (BsonDocument doc in matchedObjects) //Cursor only works through iterating it seems. Ben? I may be wrong about this
                {
                    BsonArray tagArray = doc["tags"].AsBsonArray;
                    foreach (string tag in tagArray)
                    {
                        foundTags.Add(tag);
                    }
                }
            }
            else
            {
				return null;
               
            }
            return foundTags;
        }

        public void enterSelectionData(SelectionData selecData)
        {
            BsonDocument doc = BsonDocument.Create(selecData);
            this.Insert(collectionName, doc);
        }

        public MongoCursor retrieveRecentSelection()
        {
            var collection = objectsDatabase.GetCollection(collectionName);
            return collection.FindAll();
        }

        //returns a cursor which points to the set of documents which match query
        public MongoCursor Get(string collectionName, IMongoQuery query)
        {
            var objectsCollection = objectsDatabase.GetCollection(collectionName);
            return objectsCollection.Find(query);
        }
        //returns a cursor which points to the set of documents where key=value
        //TODO: process results/return something else?
        //      lt/gt/ne queries
        public MongoCursor Get(string collectionName, string key, string value)
        {
            return this.Get(collectionName, Query.EQ(key, value));

        }
        //queryDict: a Dictionary of key-value pairs
        //returns a cursor which points to the set of elements which match the AND of every key-value pair
        //in queryDict
        public MongoCursor Get(string collectionName, Dictionary<string, string> queryDict)
        {
            var andList = new List<IMongoQuery>();
            foreach (KeyValuePair<string, string> entry in queryDict)
            {
                andList.Add(Query.EQ(entry.Key, entry.Value));
            }
            var query = new QueryBuilder<BsonDocument>();
            return this.Get(collectionName, query.And(andList));
        }
        /** Public interface functions for GUI/CV use **/
        //Saves object selected by gui
        //image = cropped image of object, info = key/value dict of identifying info about that object
        public void saveSelection(Image image, Dictionary<string, object> info)
        {
            var filename = RandomString(10) + ".jpg";
            bool hasName = info.ContainsKey("name");
            if (hasName)
            {
                MongoCursor priorSelections = Get(collectionName, Query.EQ("name", (string)info["name"]));
                if ((string)info["name"]=="" || priorSelections.Size() == 0) //First time seeing an object with this name
                {
                    info.Add("count", 1);
                    info.Add("timestamp", DateTime.Now);
                    InsertImage(collectionName, image, filename, info);
                }
                else //Seeing an object previously named
                {
                    foreach (BsonDocument doc in priorSelections) //Should only be one.
                    {
                        int changedCount = (int)doc["count"] + 1;
                        modifyObject("name", info["name"].ToString(), "count", changedCount);
                    }
                }
            }
            else
            {
                info.Add("name", "");
                info.Add("count", 1);
                info.Add("timestamp", DateTime.Now);
                InsertImage(collectionName, image, filename, info);
            }


           
            
        }
        //Retrieve a previous selection, where key=value
        //Returns a Dictionary containing info about the selection + the cropped image
        //if no result found, returns null
        public Dictionary<string, object> getSelection(string key, string value)
        {
            var collection = objectsDatabase.GetCollection(collectionName);
            BsonDocument matchedDoc = collection.FindOne(Query.EQ(key, value));
            if(matchedDoc!=null)
            {
                Image image = GetImage(matchedDoc);
                Dictionary<string, object> dict = matchedDoc.ToDictionary();
                dict.Add("image", image);
                return dict;
            }
            return null;
        }

       
        //Updates a selection with the given id, to have the given key/value pair in its info
        //If the selection already has a value for the given key, it will be replaced by the new
        //value, otherwise the new key will be added to the selection's info
        public void updateSelectionInfo(string id, string key, string value)
        {
            var collection = objectsDatabase.GetCollection(collectionName);
            BsonDocument matchedDoc = collection.FindOneById(new ObjectId(id));
            if(matchedDoc==null)
            {
                return;
            }
            matchedDoc.Set(key, value);
            collection.Save(matchedDoc);
            
        }
        //Updates a selection with the given id to have newInfo as it's info
        //Will completely overwrite old info for the selection
        public void updateSelectionInfo(string id, string newInfo)
        {
            var collection = objectsDatabase.GetCollection(collectionName);
            BsonDocument document = BsonDocument.Parse(newInfo);
            document.Set("_id", new ObjectId(id));
            collection.Save(document);
        }
        //Retrieves all objects that do not have a 'name' attribute
        //Returns a List containting Dictionaries of key/value pairs representing a selection
        //If no objects found without names, will return an empty list
        public List<Dictionary<string, object>> getUnnamedObjects()
        {
            List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();

            var collection = objectsDatabase.GetCollection(collectionName);

            MongoCursor cursor = collection.Find(queryFromString("{\"name\" : {\"$exists\": false}}"));
            foreach (BsonDocument document in cursor)
            {
                Image image = GetImage(document);
                Dictionary<string, object> listItem = document.ToDictionary();
                listItem.Add("image", image);
                list.Add(listItem);
            }
            return list;

        }

        private int compareDocsTimestamp(Dictionary<string, object> dict1, Dictionary<string, object> dict2)
        {
            int frequencyDiff = (int)(dict1["count"]) - (int)(dict2["count"]);
            double timeDiff = ((DateTime)(dict1["timestamp"]) - (DateTime)(dict2["timestamp"])).TotalDays;

            return (int)Math.Floor(0.5 * frequencyDiff + 0.5 * timeDiff);



        }
        //given a list of tags describing a current context, return a list of objects that are tagged
        //with these tags
        public List<Dictionary<string, object>> getLikelyObjects(List<string> tags, int maxResults = 99)
        {
            List<Dictionary<string, object>> likelyObjects = new List<Dictionary<string, object>>();

            var collection = objectsDatabase.GetCollection(collectionName);
            var query = queryFromString("{tags:{ $in: " + tags.ToString() + "}}");

            MongoCursor docsFound = Get(collectionName, query);
            foreach(BsonDocument doc in docsFound)
            {
                likelyObjects.Add(doc.ToDictionary());
            }
            likelyObjects.Sort(compareDocsTimestamp);
            if(likelyObjects.Count > maxResults)
            {
                likelyObjects.RemoveRange(maxResults, likelyObjects.Count - maxResults);
            }
            

            return likelyObjects;
        }
        static int Main(string[] args)
        {
            DatabaseManager dbManager = new DatabaseManager();

            var col = dbManager.objectsDatabase.GetCollection(dbManager.collectionName);
            //col.RemoveAll(); //Clearing out for testing temporarily

            dbManager.Insert(dbManager.collectionName, new BsonDocument{
            {"test", "hello"},
            {"author", "ben"}
        });
            dbManager.Insert(dbManager.collectionName, new BsonDocument{
            {"test", "hello"},
            {"author", "zach"}
        });
            dbManager.Insert(dbManager.collectionName, new BsonDocument{
            {"test", "nm u"},
            {"author", "ben"}
        });
            var queryDict = new Dictionary<string, string>()
        {
            {"test", "hello"},
            {"author", "ben"}
        };

            dbManager.Insert(dbManager.collectionName, new BsonDocument {
                {"id", "1"},
                {"time", "12"}
            });

            dbManager.Insert(dbManager.collectionName, new BsonDocument {
                {"id", "2"},
                {"time", "13"}
            });

            System.Console.Write(dbManager.Get(dbManager.collectionName, queryDict).Count() + "\n");
            queryDict.Add("nothing", "45");
            System.Console.Write(dbManager.Get(dbManager.collectionName, queryDict).Count());

            dbManager.modifyObject("id", "1", "time", "17");
            dbManager.modifyObject("id", "2", "time", "19");
           
           
            Image twodollar = Image.FromFile("\\\\engin-labs.m.storage.umich.edu\\zahuston\\windat.v2\\Desktop\\2-dollar-bill.jpg");
            Dictionary<string, object> dollarDictionary = new Dictionary<string, object>();
            dollarDictionary.Add("name", "twodollar");
            dbManager.saveSelection(twodollar, dollarDictionary);
//            Image fivedollar = Image.FromFile("C:\\Users\\Ben\\Desktop\\New_five_dollar_bill.jpg");
//            dbManager.InsertImage("test_collection", onedollar, "onedollar.jpg", "{'president': 'George Washington', 'value': '1'}");
//            dbManager.InsertImage("test_collection", fivedollar, "fivedollar.jpg", "{'president': 'Abraham Lincoln', 'value': '5'}");
            var output_img = dbManager.GetImage("test_collection", "filename", "onedollar.jpg");
            // output_img.Save("C:\\Users\\Ben\\Desktop\\new_output.jpg");
            System.Console.Write(output_img);

            return 0;
        }
    }
}
