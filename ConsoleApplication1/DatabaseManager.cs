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

/**
 * TODO:
 * If objects other than strings need to be stored, create necessary methods
 * and serialization map: http://docs.mongodb.org/ecosystem/tutorial/serialize-documents-with-the-csharp-driver/
 * 
 * Image storing/retrieving
 * Some useful links:
 * http://docs.mongodb.org/manual/core/gridfs/
 * http://stackoverflow.com/questions/4988436/mongodb-gridfs-with-c-how-to-store-files-such-as-images
 * 
**/


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
        MongoClient client = new MongoClient();
        server = client.GetServer();
        objectsDatabase = server.GetDatabase("choices_db");

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
    //Right now it's built using a filename string containing a path to the image on disk
    public void InsertImage(string collectionName, string filename, string details)
    {
        using (var fs = new FileStream(filename, FileMode.Open))
        {
          
            var gridFsInfo = objectsDatabase.GridFS.Upload(fs, filename);
            var fileId = gridFsInfo.Id;

            var collection = objectsDatabase.GetCollection(collectionName);
            BsonDocument bson = BsonDocument.Parse(details);
            bson.Add(new BsonElement("filename", filename));
            this.Insert(collectionName, bson);
        }

    }
    public void GetImage(string collectionName, string key, string value )
    {
        var cursor = this.Get(collectionName, key, value);
        foreach(BsonDocument document in cursor)
        {
            var filename = document["filename"];
            System.Console.Write(filename);
            var file = objectsDatabase.GridFS.FindOne(Query.EQ("filename", filename));
            System.Console.Write("Found file:");
            System.Console.Write(file);
            using (var stream = file.OpenRead())
            {
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, (int)stream.Length);
                using (var newFs = new FileStream("C:\\Users\\Ben\\Desktop\\output_img.png", FileMode.Create))
                {
                    newFs.Write(bytes, 0, bytes.Length);
                }
            }
        }

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
        foreach(KeyValuePair<string, string> entry in queryDict)
        {
            andList.Add(Query.EQ(entry.Key, entry.Value));
        }
        var query = new QueryBuilder<BsonDocument>();
        return this.Get(collectionName, query.And(andList));
    }

    

    static int Main(string[] args)
    {
        DatabaseManager dbManager = new DatabaseManager();
        dbManager.Insert("test_collection", new BsonDocument{
            {"test", "hello"},
            {"author", "ben"}
        });
        dbManager.Insert("test_collection", new BsonDocument{
            {"test", "hello"},
            {"author", "zach"}
        });
        dbManager.Insert("test_collection", new BsonDocument{
            {"test", "nm u"},
            {"author", "ben"}
        });
        var queryDict = new Dictionary<string, string>()
        {
            {"test", "hello"},
            {"author", "ben"}
        };

        System.Console.Write(dbManager.Get("test_collection", queryDict).Count()+"\n");
        queryDict.Add("nothing", "45");
        System.Console.Write(dbManager.Get("test_collection", queryDict).Count());


        dbManager.InsertImage("test_collection", "C:\\Users\\Ben\\Desktop\\dollar-bill-2.jpg", "{'president': 'George Washington', 'value': '1'}");
        dbManager.InsertImage("test_collection", "C:\\Users\\Ben\\Desktop\\New_five_dollar_bill.jpg", "{'president': 'Abraham Lincoln', 'value': '5'}");
        dbManager.GetImage("test_collection", "value", "1");
        return 0;
    }
}

