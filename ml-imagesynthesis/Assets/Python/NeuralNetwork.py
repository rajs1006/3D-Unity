import matplotlib.image as mpimg
import io
import numpy as np

from detector import Detector


class NNFacade():
    
    def __init__(self):
        pass
            
    def execute(self, message, draw=False):
        
        train = True if len(message) == 4 else False
       
        image = NNConnector.fileToImage(message[0])
        gtKeyPoints = NNConnector.byteToKPs(message[1])
        cvKeyPoints = NNConnector.byteToKPs(message[2])
            
        test_pts = Detector(image, gtKeyPoints, cvKeyPoints, train)     
        return np.array2string(test_pts(draw))
            
         

class NNConnector():
    
    def byteToImage(dimensions, encodedImage):
        dims  = np.array(dimensions.decode().split(','), dtype=int)
        image = Image.frombytes('RGBA',(dims[0], dims[1]),encodedImage, 'raw')
        return image
    
    def fileToImage(filename):
        image = mpimg.imread(filename)
        return image
    
    def byteToKPs(keypoints, decodeType=None):
        
        if decodeType is None:
            keypoints = keypoints.decode()
        else:
            keypoints = keypoints.decode(decodeType)
            
        KPs = keypoints.split("\n")
        KPs = KPs if KPs[-1] else KPs[:-1]
        
        finalKPs = np.zeros((len(KPs), 2))
        for i, kp in enumerate(KPs):
            finalKPs[i] = np.array(kp.split(','))
        
        return finalKPs