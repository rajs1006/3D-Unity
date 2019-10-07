import numpy as np
import cv2

import torch
from torchvision import transforms, utils
from torch.utils.data import Dataset, IterableDataset

class KeypointsIterableDataset(IterableDataset):
    """Face Landmarks dataset."""

    def __init__(self,image, keypoints, transform=None):
        """
        Args:
            csv_file (string): Path to the csv file with annotations.
            root_dir (string): Directory with all the images.
            transform (callable, optional): Optional transform to be applied
                on a sample.
        """
        self.transform = transform
        self.image = image
        self.keypoints = keypoints

    def __iter__(self):
        self.iter = 1
        
        if(self.image.shape[2] == 4):
                self.image = self.image[:,:,0:3]

        sample = {'image': self.image, 'keypoints': self.keypoints.astype('float')}
        if self.transform:
            sample = self.transform(sample)
        self.sample = sample
        
        return self
    
    def __next__(self):
        if self.iter <= 1:
            self.iter += 1
            return self.sample
        raise StopIteration

class KeypointsDataset(Dataset):
    """Face Landmarks dataset."""

    def __init__(self, root_dir = 'Sytheticdata/ml-imagesynthesis/captures', folder="Train/" , kp_file = 'image_%05d_img', transform=None, length=5):
        """
        Args:
            csv_file (string): Path to the csv file with annotations.
            root_dir (string): Directory with all the images.
            transform (callable, optional): Optional transform to be applied
                on a sample.
        """
        self.root_dir = os.path.join(path, root_dir)
        self.key_pts_file = os.path.join(folder, kp_file)
        self.transform = transform
        
        files = os.listdir(os.path.join(self.root_dir, folder))
        self.dataLen = int(len(files)/length)
        
    def __len__(self):
        return self.dataLen

    def __getitem__(self, idx):
        
        orb_image = mpimg.imread(os.path.join(self.root_dir, "{}-kp.png".format(self.key_pts_file %idx)))
        if(orb_image.shape[2] == 4):
                orb_image = orb_image[:,:,0:3]

        
        image = mpimg.imread(os.path.join(self.root_dir, "{}.png".format(self.key_pts_file %idx)))
        if(image.shape[2] == 4):
                image = image[:,:,0:3]

        KPs = pd.read_csv(os.path.join(self.root_dir, "{}-GT.txt".format(self.key_pts_file %idx)), header=None).as_matrix()

        sample = {'orb_image':orb_image, 'image': image, 'keypoints': KPs.astype('float')}
        
        if self.transform:
            sample = self.transform(sample)

        return sample
    
# tranforms
class Normalize(object):
    """Convert a color image to grayscale and normalize the color range to [0,1]."""        

    def __call__(self, sample):
        image, key_pts = sample['image'], sample['keypoints']
        
        image_copy = np.copy(image)
        key_pts_copy = np.copy(key_pts)

        # convert image to grayscale
        image_copy = cv2.cvtColor(image, cv2.COLOR_RGB2GRAY)
        
        # scale color range from [0, 255] to [0, 1]
        image_copy=  image_copy/255.0
        
        # scale keypoints to be centered around 0 with a range of [-1, 1]
        # mean = 100, sqrt = 50, so, pts should be (pts - 100)/50
        key_pts_copy = (key_pts_copy - 100)/50.0


        return {'image': image_copy, 'keypoints': key_pts_copy, 'scale' : sample['scale'], 'crop' : sample['crop']}


class Rescale(object):
    """Rescale the image in a sample to a given size.

    Args:
        output_size (tuple or int): Desired output size. If tuple, output is
            matched to output_size. If int, smaller of image edges is matched
            to output_size keeping aspect ratio the same.
    """

    def __init__(self, output_size):
        assert isinstance(output_size, (int, tuple))
        self.output_size = output_size

    def __call__(self, sample):
        image, key_pts = sample['image'], sample['keypoints']

        h, w = image.shape[:2]
        if isinstance(self.output_size, int):
            if h > w:
                new_h, new_w = self.output_size * h / w, self.output_size
            else:
                new_h, new_w = self.output_size, self.output_size * w / h
        else:
            new_h, new_w = self.output_size

        new_h, new_w = int(new_h), int(new_w)

        img = cv2.resize(image, (new_w, new_h))
        # scale the pts, too
        key_pts = key_pts * [new_w / w, new_h / h]
        
        return {'image': img, 'keypoints': key_pts, 'scale' : torch.from_numpy(np.array([new_w / w, new_h / h]))}


class RandomCrop(object):
    """Crop randomly the image in a sample.

    Args:
        output_size (tuple or int): Desired output size. If int, square crop
            is made.
    """

    def __init__(self, output_size):
        assert isinstance(output_size, (int, tuple))
        if isinstance(output_size, int):
            self.output_size = (output_size, output_size)
        else:
            assert len(output_size) == 2
            self.output_size = output_size

    def __call__(self, sample):
        image, key_pts = sample['image'], sample['keypoints']

        h, w = image.shape[:2]
        new_h, new_w = self.output_size

        top = np.random.randint(0, h - new_h)
        left = np.random.randint(0, w - new_w)

        image = image[top: top + new_h,
                      left: left + new_w]

        key_pts = key_pts - [left, top]

        return {'image': image, 'keypoints': key_pts, 'scale' : sample['scale'], 'crop' : torch.from_numpy(np.array([left, top]))}


class ToTensor(object):
    """Convert ndarrays in sample to Tensors."""

    def __call__(self, sample):
        image, key_pts = sample['image'], sample['keypoints']
         
        # if image has no grayscale color channel, add one
        if(len(image.shape) == 2):
            # add that third color dim
            image = image.reshape(image.shape[0], image.shape[1], 1)
            
        # swap color axis because
        # numpy image: H x W x C
        # torch image: C X H X W
        image = image.transpose((2, 0, 1))
        
        return {'image': torch.from_numpy(image),
                'keypoints': torch.from_numpy(key_pts),'scale' :sample['scale'], 'crop' : sample['crop']}
    
class InverseTransform(object):
    """Convert ndarrays in sample to Tensors."""

    def __call__(self, sample):
        key_pts, crop, scale = sample['output_pts'], sample['crop'], sample['scale']
         
        crop = crop.data.numpy()
        key_pts = key_pts.data.numpy()
        scale = scale.data.numpy()
        
        key_pts = np.squeeze(key_pts)
        key_pts = (key_pts*50.0)+100
        key_pts = (key_pts + crop)/scale
        
        return key_pts
