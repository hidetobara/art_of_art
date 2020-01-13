import os,sys,string,traceback,random,glob,json,datetime
from flask import Flask, render_template, request, send_from_directory, redirect
from PIL import Image, ImageFilter, ImageOps

import torch
import torch.nn as nn
from torchvision import transforms
from torchvision.utils import save_image
import net
from function import adaptive_instance_normalization, single_adaptive_instance_normalization, correct_adaptive_instance_normalization
from function import coral

import Store

os.environ["GOOGLE_APPLICATION_CREDENTIALS"] = './private/art-of-art-d539cb4f9b4c.json'
from google.cloud import storage

# transter main
def test_transform(size):
    transform_list = []
    if size <= 0:
        size = 512
    transform_list.append(transforms.Resize(size))
    transform_list.append(transforms.ToTensor())
    transform = transforms.Compose(transform_list)
    return transform

def style_transfer(vgg, decoder, abstracter, corrector, content):
    content_f = vgg(content)
    style_gen = abstracter.execute(content_f)
    correct_f = corrector.execute(content_f)
    feat = correct_adaptive_instance_normalization(content_f, style_gen, correct_f)
    return decoder(feat)

def run_transter(args):
    device = torch.device("cpu")
    decoder = net.decoder
    vgg = net.vgg
    abstracter = net.Abstracter2()
    corrector = net.Corrector()

    decoder.eval()
    vgg.eval()
    abstracter.eval()
    corrector.eval()

    decoder.load_state_dict(torch.load(args.decoder))
    vgg.load_state_dict(torch.load(args.vgg))
    vgg = nn.Sequential(*list(vgg.children())[:31])
    abstracter.load(args.abstracter)
    corrector.load(args.corrector)

    vgg.to(device)
    decoder.to(device)
    abstracter.to(device)
    corrector.to(device)

    content_tf = test_transform(args.content_size)

    content = content_tf(Image.open(args.in_path))
    content = content.to(device).unsqueeze(0)
    with torch.no_grad():
        output = style_transfer(vgg, decoder, abstracter, corrector, content)
    save_image(output.cpu(), args.out_path)

    client = storage.Client()
    bucket = client.get_bucket(env.bucket_id)
    blob = bucket.blob(args.blob_path)
    result = blob.upload_from_filename(filename=args.out_path)
    blob.make_public()
    return blob

def storage_blobs(head):
    client = storage.Client()
    bucket = client.get_bucket(env.bucket_id)
    blobs = []
    for b in bucket.list_blobs(prefix=head):
        blobs.append({'name':b.name, 'url':b.public_url})
    return blobs

# web main
class Env:
    def __init__(self):
        self.tmp = "/tmp/"
        self.bucket_id = 'art-transfer'

class Args:
    def __init__(self, mode='js'):
        if mode not in ['js','azs','klmt','ink']: mode = 'js'
        self.mode = mode
        self.vgg = "models/vgg_normalised.pth"
        self.decoder = "models/decoder_{}.pth.tar".format(mode)
        self.abstracter = "models/abstracter_{}.pth.tar".format(mode)
        self.corrector = "models/corrector_{}.pth.tar".format(mode)
        self.content_size = 256
        self.basename = None
        self.in_path = None
        self.out_path = None
        self.blob_path = None

now = datetime.datetime.now()
last_month = datetime.datetime.now().replace(day=1) - datetime.timedelta(days=2)
env = Env()
store = Store.Store()
app = Flask(__name__)

def random_name(n):
   randlst = [random.choice(string.ascii_letters + string.digits) for i in range(n)]
   return ''.join(randlst)

@app.route('/')
def get_index():
    context = {"title":"人工絵描きさん"}
    blobs = storage_blobs(head=now.strftime("%Y/%m/"))
    blobs.extend( storage_blobs(head=last_month.strftime("%Y/%m/")) )
    if type(blobs) is list:
        random.shuffle(blobs)
    paths = []
    for b in blobs:
        paths.append(b)
        if len(paths) >= 3: break
    context['images'] = paths
    return render_template('index.html', **context)

@app.route('/img/<filename>', methods=['GET'])
def get_img(filename):
    return send_from_directory(env.tmp, filename)

@app.route('/transfer', methods=['POST'])
def post_transger():
    try:
        if request.method != 'POST':
            return "INVALID POST."
        if 'target' not in request.files:
            return "Empty files."
        mode = request.form["mode"]
        target = request.files['target']
        a = Args(mode)
        a.basename = random_name(4)
        a.in_path = os.path.join(env.tmp, a.basename + "_i.jpg")
        out_filename = a.basename + "_o.jpg"
        a.out_path = os.path.join(env.tmp, out_filename)
        a.blob_path = '{}/{}.jpg'.format(now.strftime("%Y/%m"), a.basename)
        print("transter>", out_filename, mode)
        target.save(a.in_path)
        b = run_transter(a)
        store.record_transfer(a.blob_path, None, a.mode)
        return redirect(b.public_url)
    except Exception as ex:
        print(ex)
        traceback.print_exc()
        return "ERROR=" + str(ex), 500

@app.route('/timeline.json', methods=['GET'])
def get_timeline():
    blobs = storage_blobs(head=now.strftime("%Y/%m/"))
    return json.dumps( {'result':'ok', 'paths':blobs} )

@app.route('/like', methods=['POST','GET'])
def get_like():
    try:
        if request.method == 'POST':
            user = request.form["user"] if "user" in request.form else None
            path = request.form["path"]
        else:
            user = request.args.get("user", default=None)
            path = request.args.get("path")
        if store.exist_transfer(path):
            store.record_favorite(path, user)
            return json.dumps( {'result':'ok'} )
        else:
            return json.dumps( {'result':'fail', 'error':'NOT found'} )
    except Exception as ex:
        traceback.print_exc()
        return json.dumps( {'result':'fail', 'error':str(ex)} )

@app.route('/favorites.json', methods=['GET'])
def get_favorites():
    try:
        return json.dumps( {'result':'ok', 'items':store.collect_favorites()} )
    except Exception as ex:
        return json.dumps( {'result':'fail', 'error':str(ex)} )

@app.route('/info', methods=['GET'])
def get_info():
    array = []
    for p in glob.glob('./*'):
        array.append(p)
    for p in glob.glob('./models/*'):
        array.append('models=' + p)
    for p in glob.glob('./templates/*'):
        array.append('templates=' + p)
    return "\n".join(array)

if __name__ == "__main__":
    os.makedirs(env.tmp, exist_ok=True)
    os.chmod(env.tmp, 0o777)
    app.run(debug=True,host='0.0.0.0',port=8080)
