using System;
using MongoDB.Bson;
using MongoDB.Driver;

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


public class Movie
{
    public BsonObjectId Id { get; set; }
    public string Title { get; set; }
    public string Year { get; set; }
    public List<string> Actors { get; set; }

    public void AddActor(string actor)
    {
        if (Actors == null)
        {
            Actors = new List<string>();
        }
        Actors.Add(actor);
    }
}

public class DatabaseManager
{
    private MongoServer server;
    private MongoDatabase choicesDatabase;
   // private MongoCollection recognizedObjects;
	public DatabaseManager()
	{
        Connect();
        InsertData();
	}

    public void InsertChoice(List<string> objectsToAdd) 
    {
        var collection1 = new Movie { Title = "Vision With Grace Documentary", Year = "2013" };
        var recognizedObjects = choicesDatabase.GetCollection<string>("recognizedObjects");
      
        foreach (string objectToAdd in objectsToAdd)
        {
            if (!String.IsNullOrWhiteSpace(objectToAdd))
            {
                BsonDocument doc = BsonDocument.Parse(objectToAdd);
                recognizedObjects.Insert(doc);

            }
                
        }

    }

    private void Connect()
    {
        MongoClient client = new MongoClient();
        server = MongoServer.Create();
        choicesDatabase = server.GetDatabase("choices_db");
       
    }

    public void retrieveEntries()
    {
        var collection = choicesDatabase.GetCollection<string>("recognizedObjects");
        var backpacks = Query.EQ("Actors", "backpack");
        foreach (string name in collection.FindAs<string>(backpacks))
        {
            Console.Write(name);
        }
        
    }

    private void InsertData()
    {
       
    }

    static int Main(string[] args)
    {
        var thingsToAdd = new List<string>();
        thingsToAdd.Add("{'string': 'backpack'}");
        thingsToAdd.Add("{'string': 'pencil case'}");
        DatabaseManager mgr = new DatabaseManager();
        mgr.InsertChoice(thingsToAdd);
        mgr.retrieveEntries();
        return 0;
    }
}

