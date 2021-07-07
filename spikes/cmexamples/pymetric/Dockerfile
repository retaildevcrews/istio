FROM python:3.6

RUN pip install tornado

RUN pip install psutil

COPY server.py .

expose 32888

CMD [ "python","server.py" ]
