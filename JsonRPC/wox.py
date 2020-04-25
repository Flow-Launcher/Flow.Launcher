# -*- coding: utf-8 -*-
from __future__ import print_function
import json
import sys
import inspect

class FlowLauncher(object):
    """
    Flow.Launcher python plugin base
    """

    def __init__(self):
        rpc_request = json.loads(sys.argv[1])
        # proxy is not working now
        self.proxy = rpc_request.get("proxy",{})
        request_method_name = rpc_request.get("method")
        request_parameters = rpc_request.get("parameters")
        methods = inspect.getmembers(self, predicate=inspect.ismethod)

        request_method = dict(methods)[request_method_name]
        results = request_method(*request_parameters)

        if request_method_name == "query" or request_method_name == "context_menu":
            print(json.dumps({"result": results}))

    def query(self,query):
        """
        sub class need to override this method
        """
        return []

    def context_menu(self, data):
        """
        optional context menu entries for a result
        """
        return []

    def debug(self,msg):
        """
        alert msg
        """
        print("DEBUG:{}".format(msg))
        sys.exit()

class FlowLauncherAPI(object):

    @classmethod
    def change_query(cls,query,requery = False):
        """
        change flowlauncher query
        """
        print(json.dumps({"method": "Flow.Launcher.ChangeQuery","parameters":[query,requery]}))

    @classmethod
    def shell_run(cls,cmd):
        """
        run shell commands
        """
        print(json.dumps({"method": "Flow.Launcher.ShellRun","parameters":[cmd]}))

    @classmethod
    def close_app(cls):
        """
        close flowlauncher
        """
        print(json.dumps({"method": "Flow.Launcher.CloseApp","parameters":[]}))

    @classmethod
    def hide_app(cls):
        """
        hide flowlauncher
        """
        print(json.dumps({"method": "Flow.Launcher.HideApp","parameters":[]}))

    @classmethod
    def show_app(cls):
        """
        show flowlauncher
        """
        print(json.dumps({"method": "Flow.Launcher.ShowApp","parameters":[]}))

    @classmethod
    def show_msg(cls,title,sub_title,ico_path=""):
        """
        show messagebox
        """
        print(json.dumps({"method": "Flow.Launcher.ShowMsg","parameters":[title,sub_title,ico_path]}))

    @classmethod
    def open_setting_dialog(cls):
        """
        open setting dialog
        """
        print(json.dumps({"method": "Flow.Launcher.OpenSettingDialog","parameters":[]}))

    @classmethod
    def start_loadingbar(cls):
        """
        start loading animation in flowlauncher
        """
        print(json.dumps({"method": "Flow.Launcher.StartLoadingBar","parameters":[]}))

    @classmethod
    def stop_loadingbar(cls):
        """
        stop loading animation in flowlauncher
        """
        print(json.dumps({"method": "Flow.Launcher.StopLoadingBar","parameters":[]}))

    @classmethod
    def reload_plugins(cls):
        """
        reload all flowlauncher plugins
        """
        print(json.dumps({"method": "Flow.Launcher.ReloadPlugins","parameters":[]}))
