import tornado.ioloop
import tornado.web
import psutil

class MainHandler(tornado.web.RequestHandler):
    def get(self):
        self.write({"cpuavg":max([x / psutil.cpu_count() * 100 for x in psutil.getloadavg()]),
            "numcpu":psutil.cpu_count()})

def make_app():
    return tornado.web.Application([
        (r'/.*', MainHandler),
    ])

if __name__ == "__main__":
    app = make_app()
    app.listen(32888)
    tornado.ioloop.IOLoop.current().start()
