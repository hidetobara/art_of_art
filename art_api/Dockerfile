
FROM python:3.5

RUN apt-get update -y && apt-get upgrade -y && \
	apt-get -y install python3-pip vim wget supervisor less
RUN pip3 install tensorboardx flask gunicorn pillow torch==0.4.0
RUN pip3 install torchvision==0.2.1 --no-deps
RUN pip3 install google-cloud-storage google-cloud-firestore firebase-admin

ENV APP_HOME /app
WORKDIR $APP_HOME
COPY . $APP_HOME

CMD exec gunicorn --bind :8080 --workers 1 --threads 2 app:app
