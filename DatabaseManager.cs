﻿using System;
using System.Collections.Generic;
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
	public DatabaseManager()
	{
        Connect();
        InsertData();
	}
    private void Connect()
    {
        server = MongoServer.Create();
        moviesDatabase = server.GetDatabase("choices_db");
    }
}
