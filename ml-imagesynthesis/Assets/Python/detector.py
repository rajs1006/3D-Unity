import os
import glob
import numpy as np
import pandas as pd

from sklearn.metrics import mean_squared_error

import matplotlib.pyplot as plt
import matplotlib.image as mpimg

import torch
from torch.utils.data import DataLoader
from torchvision import transforms, utils

from Model import Net, train_net, test_net
from DataUtils import KeypointsIterableDataset, Rescale, RandomCrop, Normalize, ToTensor, InverseTransform


class Detector():
    
    def __init__(self, image, keypoints, initialKeypoints, train):
        
        self.image = image
        self.keypoints = keypoints
        self.initialKeypoints = initialKeypoints
        
        self.train = train
        
    def __call__(self, draw = False):
        
        # Load model
        net = Net()
        # Trained model
        net.load_state_dict(torch.load('./saved_models/model_checkpoint_kpd.pt')['model'])
        ## print out your net and prepare it for testing (uncomment the line below)
        net.eval()
        
        ## Data preparation
        transformations = transforms.Compose([Rescale(250),
                                             RandomCrop(224),
                                             Normalize(),
                                             ToTensor()])

        # create the transformed dataset
        transformed_data = KeypointsIterableDataset(self.image, self.keypoints, transform=transformations)
        data_loader = DataLoader(transformed_data, num_workers=0)
        
        ## if train flag is set, Start training picking up old checkpoint
        if self.train:
            print("Training...")
            ## Run each record twice for training.
            n_epochs = 2
            train_net(n_epochs, data_loader, net)
        
        ## Get the prediction
        print("Predicting...")
        test_images, test_pts, gt_pts, sample = test_net(data_loader, net)
    
        if draw:
            visualize_output(test_images, test_pts)
        # Rescaled.
        return InverseTransform()(sample)

    
def visualize_output(test_images, test_outputs, gt_pts=None, batch_size=1):

    for i in range(0, batch_size):
        plt.figure(figsize=(20,10))
        #ax = plt.subplot(i+1, 1, i+1)

        # un-transform the image data
        image = test_images[i].data   # get the image from it's Variable wrapper
        image = image.numpy()   # convert to numpy array from a Tensor
        image = np.transpose(image, (1, 2, 0))   # transpose to go from torch to numpy image

        # un-transform the predicted key_pts data
        predicted_key_pts = test_outputs[i].data
        predicted_key_pts = predicted_key_pts.numpy()
        # undo normalization of keypoints  
        predicted_key_pts = predicted_key_pts*50.0+100
        
        # plot ground truth points for comparison, if they exist
        ground_truth_pts = None
        if gt_pts is not None:
            ground_truth_pts = gt_pts[i]         
            ground_truth_pts = ground_truth_pts*50.0+100
        
        # call show_all_keypoints
        show_all_keypoints(np.squeeze(image), predicted_key_pts, ground_truth_pts,fileName = 'Output-{}.png'.format(i))
    
    plt.show()
        
def show_all_keypoints(image, predicted_key_pts, gt_pts=None, fileName = None):
    """Show image with predicted keypoints"""
    # image is grayscale
    plt.imshow(image, cmap='gray')
    plt.plot(predicted_key_pts[:, 0], predicted_key_pts[:, 1], c='m', label='Predicted')