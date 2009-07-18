// 
// XmlSerializer.cs
//  
// Author:
//       Scott Peterson <lunchtimemama@gmail.com>
// 
// Copyright (c) 2009 Scott Peterson
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

using Mono.Upnp.Xml.Compilation;

namespace Mono.Upnp.Xml
{
    public sealed class XmlSerializer
    {
        struct Nothing
        {
        }
        
        readonly XmlSerializer<Nothing> serializer = new XmlSerializer<Nothing> ();
        
        public void Serialize<TObject> (TObject obj, XmlWriter writer)
        {
            serializer.Serialize (obj, writer, new Nothing ());
        }
        
        public void Serialize<TObject> (TObject obj, Stream stream)
        {
            serializer.Serialize (obj, stream, new Nothing ());
        }
        
        public byte[] GetBytes<TObject> (TObject obj)
        {
            return serializer.GetBytes (obj, new Nothing ());
        }
        
        public byte[] GetBytes<TObject> (TObject obj, Encoding encoding)
        {
            return serializer.GetBytes (obj, new Nothing (), encoding);
        }
        
        public string GetString<TObject> (TObject obj)
        {
            return serializer.GetString (obj, new Nothing ());
        }
    }
    
    public sealed class XmlSerializer<TContext>
    {
        static UTF8Encoding utf8 = new UTF8Encoding (false);
        
        readonly SerializationCompilerFactory<TContext> compiler_factory;
        readonly Dictionary<Type, SerializationCompiler<TContext>> compilers = new Dictionary<Type, SerializationCompiler<TContext>> ();
        
        public XmlSerializer ()
            : this (null)
        {
        }
        
        public XmlSerializer (SerializationCompilerFactory<TContext> compilerFactory)
        {
            this.compiler_factory = compilerFactory ?? new DelegateSerializationCompilerFactory<TContext> ();
        }
        
        public void Serialize<TObject> (TObject obj, XmlWriter writer, TContext context)
        {
            if (writer == null) throw new ArgumentNullException ("writer");
            
            SerializeCore (obj, writer, context);
        }
        
        public void Serialize<TObject> (TObject obj, Stream stream, TContext context)
        {
            if (stream == null) throw new ArgumentNullException ("stream");
            
            using (var writer = XmlWriter.Create (stream, new XmlWriterSettings { Encoding = utf8 })) {
                SerializeCore (obj, writer, context);
            }
        }
        
        public byte[] GetBytes<TObject> (TObject obj, TContext context)
        {
            return GetBytes<TObject> (obj, context, null);
        }
                
        public byte[] GetBytes<TObject> (TObject obj, TContext context, Encoding encoding)
        {
            using (var stream = new MemoryStream ()) {
                using (var writer = XmlWriter.Create (stream, new XmlWriterSettings { Encoding = encoding ?? utf8 })) {
                    writer.WriteProcessingInstruction ("xml", @"version=""1.0""");
                    SerializeCore (obj, writer, context);
                }
                return stream.ToArray ();
            }
        }
        
        public string GetString<TObject> (TObject obj, TContext context)
        {
            return utf8.GetString (GetBytes (obj, context, utf8));
        }
        
        void SerializeCore<TObject> (TObject obj, XmlWriter writer, TContext context)
        {
            if (obj == null) throw new ArgumentNullException ("obj");
            
            var serializer = GetCompilerForType (typeof (TObject)).TypeSerializer;
            serializer (obj, new XmlSerializationContext<TContext> (this, writer, context));
        }

        internal void AutoSerializeObjectAndMembers<TObject> (TObject obj, XmlSerializationContext<TContext> context)
        {
            var serializer = GetCompilerForType (typeof (TObject)).TypeAutoSerializer;
            serializer (obj, context);
        }
        
        internal void AutoSerializeMembersOnly<TObject> (TObject obj, XmlSerializationContext<TContext> context)
        {
            var serialzer = GetCompilerForType (typeof (TObject)).MemberAutoSerializer;
            serialzer (obj, context);
        }
        
        internal SerializationCompiler<TContext> GetCompilerForType (Type type)
        {
            SerializationCompiler<TContext> compiler;
            if (!compilers.TryGetValue (type, out compiler)) {
                compiler = compiler_factory.CreateSerializationCompiler (this, type);
                compilers[type] = compiler;
            }
            return compiler;
        }
    }
}
