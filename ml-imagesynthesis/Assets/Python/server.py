import time
import zmq
from argparse import ArgumentParser

from NeuralNetwork import NNFacade, NNConnector


def main():
    
    context = zmq.Context()
    socket = context.socket(zmq.REP)
    socket.bind("tcp://*:5000")

    print("Socket is bound...")
    
    nnFacade = NNFacade()

    while True:
        #  Wait for next request from client
        message = socket.recv_multipart()
        print("Request received... : %s" % message[0])
        
        retMessage = nnFacade.execute(message)
        #  Do some 'work'.
        #  Try reducing sleep time to 0.01 to see how blazingly fast it communicates
        #  In the real world usage, you just need to replace time.sleep() with
        #  whatever work you want python to do, maybe a machine learning task?
        time.sleep(0.1)

        #  Send reply back to client
        #  In the real world usage, after you finish your work, send your output here
        socket.send_string(retMessage)
        print("Request returned...: %s" % message[0])

main()

