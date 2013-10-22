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
    public class SelectionData
    {
        [BsonId]
        public int objectID { get; set; }
        [BsonElement]
        public string context { get; set; }

        [BsonConstructor]
        public SelectionData()
        {
            this.objectID = -1;
            this.context = "";
        }

        [BsonConstructor]
        public SelectionData(int id, string ctx)
        {
            this.objectID = id;
            this.context = ctx;
        }
    }
}
