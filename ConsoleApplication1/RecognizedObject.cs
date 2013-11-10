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

namespace DatabaseModule
{
    public class RecognizedObject
    {
        [BsonId]
        public int objectID { get; set;}
        [BsonElement]
        public string objectName { get; set;}

        [BsonConstructor]
	    public RecognizedObject()
	    {
            this.objectID = -1;
            this.objectName = "";
	    }

        [BsonConstructor]
        public RecognizedObject(int id, string name)
        {
            this.objectID = id;
            this.objectName = name;
        }
    }
}
