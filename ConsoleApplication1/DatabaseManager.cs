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
        // private MongoCollection recognizedObjects;
        public DatabaseManager()
        {
            Connect();
        }

        private void Connect()
        {
            const string remoteConnectionString = "mongodb://localhost";
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
        public void InsertImage(string collectionName, Image image, string filename, string details)
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
                    bson = BsonDocument.Parse(details);
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
            this.Insert("RecognizedObjects", doc);
        }

        public void enterSelectionData(SelectionData selecData)
        {
            BsonDocument doc = BsonDocument.Create(selecData);
            this.Insert("SelectionData", doc);
        }

        public MongoCursor retrieveRecentSelection()
        {
            var collection = objectsDatabase.GetCollection("RecognizedObjects");
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
        public void saveSelection(Image image, string info)
        {
            var filename = RandomString(10) + ".jpg";
            InsertImage("selected_objects", image, filename, info);
        }
        //Retrieve a previous selection, where key=value
        //Returns a Dictionary containing info about the selection + the cropped image
        //if no result found, returns null
        public Dictionary<string, object> getSelection(string key, string value)
        {
            var collection = objectsDatabase.GetCollection("selected_objects");
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
            var collection = objectsDatabase.GetCollection("selected_objects");
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
            var collection = objectsDatabase.GetCollection("selected_objects");
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

            var collection = objectsDatabase.GetCollection("selected_objects");

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
        static int Main(string[] args)
        {
            DatabaseManager dbManager = new DatabaseManager();
            Image onedollar = Image.FromFile("C:\\Users\\Ben\\Desktop\\dollar-bill-2.jpg");
            Image fivedollar = Image.FromFile("C:\\Users\\Ben\\Desktop\\New_five_dollar_bill.jpg");
            Image tendollar = Image.FromFile("C:\\Users\\Ben\\Desktop\\ten_dollar_bill.jpg");

            string onedollarjson = "{'name': 'one dollar', 'value': '1'}";
            string fivedollarjson = "{'value': '5'}";
            string tendollarjson = "{'value': '20'}";

           // dbManager.saveSelection(onedollar, onedollarjson);
           // dbManager.saveSelection(fivedollar, fivedollarjson);
           // dbManager.saveSelection(tendollar, tendollarjson);

            var id = (ObjectId)(dbManager.getSelection("value", "20")["_id"]);
            dbManager.updateSelectionInfo(id.ToString(), "value", "10");
            
            List<Dictionary<string, object>> unnamed = dbManager.getUnnamedObjects();
            foreach(var entry in unnamed)
            {
                System.Console.Write(entry["value"]);
            }
           
            
            return 0;
        }
    }
}
