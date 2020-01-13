import sys,os,datetime
import firebase_admin
from firebase_admin import credentials
from firebase_admin import firestore

os.environ["GOOGLE_APPLICATION_CREDENTIALS"] = './private/art-of-art-d539cb4f9b4c.json'
from google.cloud import storage


class Store:
    def __init__(self):
        self.cred = credentials.Certificate("private/art-of-art-firebase-adminsdk-mxycf-49fd1df303.json")
        firebase_admin.initialize_app(self.cred)

    def record_transfer(self, path, user, mode):
        db = firestore.client()
        db.collection('transfers').add({'path':path, 'user':user, 'mode':mode})

    def record_favorite(self, path, user, value=1):
        db = firestore.client()
        db.collection('favorites').add({'path':path, 'user':user, 'evaluate':value, 'created_at':datetime.datetime.now()})

    def exist_transfer(self, path):
        db = firestore.client()
        for r in db.collection('transfers').where('path', '==', path).get():
            return True
        return False

    def collect_favorites(self):
        db = firestore.client()
        client = storage.Client()
        month1before = datetime.datetime.now() - datetime.timedelta(days=30)
        table = {}
        for r in db.collection('favorites').where('created_at', '>', month1before).get():
            try:
                h = r.to_dict()
                path = h['path']
                evaluate = h['evaluate']
                if path not in table:
                    table[path] = evaluate
                else:
                    table[path] += evaluate
            except:
                pass
        arr = sorted(table.items(), key=lambda x: -x[1])

        bucket = client.get_bucket('art-transfer')
        items = []
        for a in arr:
            b = storage.Blob(a[0], bucket)
            link = b.public_url
            items.append([link, a[1]])
            if len(items) >= 30: break
        return items

