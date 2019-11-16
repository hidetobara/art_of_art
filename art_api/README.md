# build
docker build -t art .

# run
docker run -it --rm -p 8080:8080 -v /c/obara/art_of_art:/app art /bin/bash

# push
# memory_size = 2G
gcloud builds submit --tag gcr.io/art-of-art/test --project art-of-art .

# better ?
# PROJECT_ID=art-of-art
# IMAGE=test

docker build -t gcr.io/art-of-art/test .
gcloud docker -- push gcr.io/art-of-art/test
