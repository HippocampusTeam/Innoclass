﻿using System;
using System.Collections.Generic;
using System.Linq;
using Game.Car;
using Networking;
using UnityEngine;

namespace Game.Track {
/// <summary>
/// Singleton class managing the current track and all cars racing on it, evaluating each individual.
/// </summary>
public class TrackManager : MonoBehaviour {

	public static TrackManager instance { get; private set; }

	// Sprites for visualising best and second best cars. To be set in Unity Editor.
	[SerializeField] private Sprite bestCarSprite;
	[SerializeField] private Sprite secondBestSprite;
	[SerializeField] private Sprite normalCarSprite;
	[SerializeField] private Sprite playerCarSprite;

	private Checkpoint[] checkpoints;

	/// <summary>
	/// Car used to create new cars and to set start position.
	/// </summary>
	public CarController prototypeCar;

	[HideInInspector] public NetworkMirrorCarController networkCar;

	// Struct for storing the current cars and their position on the track.
	private class RaceCar {
		public RaceCar(CarController car = null, uint checkpointIndex = 1) {
			this.car = car;
			this.checkpointIndex = checkpointIndex;
		}
		public readonly CarController car;
		public uint checkpointIndex;
	}

	private readonly List<RaceCar> cars = new List<RaceCar>();

	/// <summary>
	/// The amount of cars currently on the track.
	/// </summary>
	private int carCount => cars.Count;

	private CarController bestCar;

	public CarController getCar(int index) {
		return cars[index].car;
	}
	
	public void removeCarAt(int index) {
		cars.RemoveAt(index);
	}

	/// <summary>
	/// The current best car (furthest in the track).
	/// </summary>
	public CarController bestCarAccessor {
		get => bestCar;
		private set {
			if (bestCar == value) return;

			// Update appearance
			if (bestCarAccessor != null)
				bestCarAccessor.setProgressSprite(normalCarSprite);
			if (value != null)
				value.setProgressSprite(bestCarSprite);

			// Set previous best to be second best now
			CarController previousBest = bestCar;
			bestCar = value;
			bestCarChanged?.Invoke(previousBest, bestCar);

			secondBestCarAccessor = previousBest;
		}
	}

	/// <summary>
	/// Event for when the best car has changed.
	/// </summary>
	public event Action<CarController, CarController> bestCarChanged;

	private CarController secondBestCar;

	/// <summary>
	/// The current second best car (furthest in the track).
	/// </summary>
	private CarController secondBestCarAccessor {
		get => secondBestCar;
		set {
			if (secondBestCarAccessor == value) return;

			if (secondBestCarAccessor != null && secondBestCarAccessor != bestCarAccessor)
				secondBestCarAccessor.setProgressSprite(normalCarSprite);
			if (value != null)
				value.setProgressSprite(secondBestSprite);

			secondBestCar = value;
		}
	}
	
	public Vector3 spawnPosition;
	public Quaternion spawnRotation;
	public uint spawnCheckpointIndex;

	/// <summary>
	/// The length of the current track in Unity units (accumulated distance between successive checkpoints).
	/// </summary>
	private float trackLength { get; set; }

	private void Awake() {
		if (instance != null) {
			Debug.LogError("Mulitple instance of TrackManager are not allowed in one Scene.");
			return;
		}

		instance = this;

		// Get all checkpoints
		checkpoints = GetComponentsInChildren<Checkpoint>();
		spawnCheckpointIndex = 1;

		// Set start position and hide prototype
		prototypeCar.gameObject.SetActive(false);

		calculateCheckpointPercentages();
	}

	// Unity method for updating the simulation
	private void Update() {
		if (Time.frameCount % 2 == 0) return;
		
		float bestScore = 0f;
		
		// Update reward for each enabled car on the track
		foreach (RaceCar car in cars.Where(car => car.car.enabled)) {
			car.car.currentCompletionReward = getCompletePerc(car.car, ref car.checkpointIndex);
			if (car.car.currentCompletionReward > bestScore) 
				bestScore = car.car.currentCompletionReward;

			// Update best
			if (bestCarAccessor == null || car.car.currentCompletionReward > bestCarAccessor.currentCompletionReward)
				bestCarAccessor = car.car;
			else if (secondBestCarAccessor == null || car.car.currentCompletionReward > secondBestCarAccessor.currentCompletionReward)
				if (bestCarAccessor != car.car) secondBestCarAccessor = car.car;
		}

		if (networkCar != null) networkCar.progressAccessor = bestScore;
	}

	public void setCarAmount(int amount) {
		if (amount < 0) throw new ArgumentException("Amount may not be less than zero.");

		if (amount == carCount) return;

		if (amount > cars.Count) // Add new cars
			for (int toBeAdded = amount - cars.Count; toBeAdded > 0; toBeAdded--) {
				GameObject carCopy = Instantiate(prototypeCar.gameObject);
				CarController controllerCopy = carCopy.GetComponent<CarController>();
				cars.Add(new RaceCar(controllerCopy));
				carCopy.SetActive(true);
				controllerCopy.movement.moveToStart();
			}
		else if (amount < cars.Count) // Remove existing cars
			for (int toBeRemoved = cars.Count - amount; toBeRemoved > 0; toBeRemoved--) {
				RaceCar last = cars[cars.Count - 1];
				cars.RemoveAt(cars.Count - 1);
				Destroy(last.car.gameObject);
			}

		if (!GameStateManager.userControl) return;
		cars.First().car.useUserInput = true;
		cars.First().car.setPlayerSprite(playerCarSprite);
		CameraManager.instance.hardTrack(cars.First().car.transform);
	}

	/// <summary>
	/// Restarts all cars and puts them at the track start.
	/// </summary>
	public void restart() {
		foreach (RaceCar car in cars) {
			if (spawnPosition.normalized.magnitude != 0) {
				car.car.movement.spawnPosition = spawnPosition;
				car.car.movement.spawnRotation = spawnRotation;
			}
			
			car.car.restart();
			car.checkpointIndex = spawnCheckpointIndex;
		}

		bestCarAccessor = null;
		secondBestCarAccessor = null;
	}

	public void stop() {
		setCarAmount(0);
	}

	/// <summary>
	/// Returns an Enumerator for iterator through all cars currently on the track.
	/// </summary>
	public IEnumerator<CarController> getCarEnumerator() {
		return cars.Select(t => t.car).GetEnumerator();
	}

	/// <summary>
	/// Calculates the percentage of the complete track a checkpoint accounts for. This method will
	/// also refresh the <see cref="trackLength"/> property.
	/// </summary>
	private void calculateCheckpointPercentages() {
		checkpoints[0].accumulatedDistance = 0; //First checkpoint is start
		//Iterate over remaining checkpoints and set distance to previous and accumulated track distance.
		for (int i = 1; i < checkpoints.Length; i++) {
			checkpoints[i].distanceToPrevious =
				Vector2.Distance(checkpoints[i].transform.position, checkpoints[i - 1].transform.position);
			checkpoints[i].accumulatedDistance = checkpoints[i - 1].accumulatedDistance + checkpoints[i].distanceToPrevious;
		}

		//Set track length to accumulated distance of last checkpoint
		trackLength = checkpoints[checkpoints.Length - 1].accumulatedDistance;

		//Calculate reward value for each checkpoint
		for (int i = 1; i < checkpoints.Length; i++) {
			checkpoints[i].rewardValue =
				checkpoints[i].accumulatedDistance / trackLength - checkpoints[i - 1].accumulatedReward;
			checkpoints[i].accumulatedReward = checkpoints[i - 1].accumulatedReward + checkpoints[i].rewardValue;
		}
	}

	// Calculates the completion percentage of given car with given completed last checkpoint.
	// This method will update the given checkpoint index accordingly to the current position.
	private float getCompletePerc(CarController car, ref uint curCheckpointIndex) {
		//Already all checkpoints captured
		if (curCheckpointIndex >= checkpoints.Length)
			return 1;

		//Calculate distance to next checkpoint
		float checkPointDistance =
			Vector2.Distance(car.transform.position, checkpoints[curCheckpointIndex].transform.position);

		//Check if checkpoint can be captured
		if (checkPointDistance <= checkpoints[curCheckpointIndex].captureRadius) {
			if (checkpoints[curCheckpointIndex].doRespawnHere) {
				spawnPosition = checkpoints[curCheckpointIndex].transform.position;
				spawnRotation = car.transform.rotation;
				spawnCheckpointIndex = curCheckpointIndex + 1;
			}
			
			curCheckpointIndex++;
			car.checkpointCaptured(); //Inform car that it captured a checkpoint
			return getCompletePerc(car, ref curCheckpointIndex); //Recursively check next checkpoint
		}

		// Return accumulated reward of last checkpoint + reward of distance to next checkpoint
		return checkpoints[curCheckpointIndex - 1].accumulatedReward +
			   checkpoints[curCheckpointIndex].getRewardValue(checkPointDistance);
	}

}
}