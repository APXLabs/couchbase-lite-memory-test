using System;
using System.Collections.Generic;
using Android.App;
using Android.Widget;
using Android.OS;
using Android.Util;
using Couchbase.Lite;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Replicator;

namespace HttpMemoryLeakCheck
{
    [Activity(Label = "HttpMemoryLeakCheck", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        private const string Tag = "ELC";
        private const string Scheme = "http";
        private const string Host = "10.2.1.125";
        private const int Port = 4984;
        private const string DbName = "couchbaseevents";

        private int _count = 1;
        private Database _db;
        private Replication _pull;
        private Replication _push;
        private Button _button;
        private TextView _info;
        private TextView _numberGenerated;
        private bool _shouldBeStopped = true;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            StartDb();
            StartReplications();

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            // Get our button from the layout resource,
            // and attach an event to it
            _button = FindViewById<Button>(Resource.Id.MyButton);

            _button.Click += OnClicked;

            _info = FindViewById<TextView>(Resource.Id.textView1);
            _info.Text = "All set up";

            _numberGenerated = FindViewById<TextView>(Resource.Id.textView2);
            _numberGenerated.Text = $"Blobs Generated:{_count}";
        }

        private void OnClicked(object sender, EventArgs eventArgs)
        {
            _button.Text = $"{_count++} clicks!";
            _shouldBeStopped = !_shouldBeStopped;
            if(!_shouldBeStopped)
                _push.Start();
        }

        private string AddDoc()
        {
            var doc = _db.CreateDocument();
            string docId = doc.Id;
            var props = new Dictionary<string, object>
            {
                {"name", "Big Party"},
                {"location", "MyHouse"},
                {"date", DateTime.Now}
            };
            try
            {
                doc.PutProperties(props);
                Log.Debug(Tag, $"doc written to database with ID = {doc.Id}");
            }
            catch (Exception e)
            {
                Log.Error(Tag, "Error putting properties to Couchbase Lite database", e);
            }
            return docId;
        }

        private void AddAttachment(string id)
        {
            var doc = _db.GetDocument(id);
            var data = MakeData(19);
            var rev = doc.CreateRevision();
            rev.SetAttachment(Guid.NewGuid().ToString(), "application/octet-stream", data);
            rev.Save(false);
            Log.Debug(Tag, $"attachment {_count} added to database on doc {doc.Id}");
        }


        private void StartDb()
        {
            _db = Manager.SharedInstance.GetDatabase("test");
        }

        private Uri CreateSyncUri()
        {
            Uri syncUri = null;
            try
            {
                var uriBuilder = new UriBuilder(Scheme, Host, Port, DbName);
                syncUri = uriBuilder.Uri;
            }
            catch (UriFormatException e)
            {
                Log.Error(Tag, "Cannot create sync uri", e);
            }
            return syncUri;
        }

        private void StartReplications()
        {
            _pull = _db.CreatePullReplication(CreateSyncUri());
            _push = _db.CreatePushReplication(CreateSyncUri());
            var authenticator = AuthenticatorFactory.CreateBasicAuthenticator("couchbase_user", "mobile");
            _pull.Authenticator = authenticator;
            _push.Authenticator = authenticator;
            _pull.Continuous = true;
      //      _push.Continuous = true;
            _pull.Start();
            _push.Start();
            _push.Changed += OnPushChanged;
        }

        private void OnPushChanged(object sender, ReplicationChangeEventArgs replicationChangeEventArgs)
        {
            switch (replicationChangeEventArgs.ReplicationStateTransition.Destination)
            {
                case ReplicationState.Stopped:
                    if (!_shouldBeStopped)
                    {
                        _count++;
                        var id = AddDoc();
                        AddAttachment(id);
                        _push.Start();
                    }
                    break;
            }

            _info.Text = replicationChangeEventArgs.ReplicationStateTransition.Destination + "\n";
            _numberGenerated.Text = $"Blobs Generated:{_count}";
        }

        /// <summary>
        /// Creates a data blob
        /// </summary>
        /// <param name="dataSize">size in MB</param>
        /// <returns></returns>
        private static byte[] MakeData(long dataSize)
        {
            var data = new byte[dataSize*1024*1024];
            return data;
        }
    }
}