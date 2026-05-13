import mongoose from 'mongoose'

const PenaltySchema = new mongoose.Schema({
  description: String,
  points:      Number,
  exerciseNum: Number,
  t:           Number,  //       
  x:           Number,  //     
  z:           Number,
}, { _id: false })

const TrackPointSchema = new mongoose.Schema({
  x:     Number,
  z:     Number,
  rot:   Number,   //   Y  
  speed: Number,
  rpm:   Number,
  t:     Number,   //     
}, { _id: false })

const AttemptSchema = new mongoose.Schema({
  studentName:       { type: String, default: '' },
  timestamp:         { type: Date,   default: Date.now },
  passed:            Boolean,
  totalPenaltyPoints: Number,
  examDuration:      Number,  // 
  exerciseStatuses:  [String], // ['Completed', 'Failed', 'Pending', ...]
  penalties:         [PenaltySchema],
  track:             [TrackPointSchema],
  lightEvents: [{
    t:        Number,
    id:       Number,
    phaseA:   String,
    phaseB:   String,
    duration: Number,
    _id: false,
  }],
  lightPositions: [{
    id: Number, x: Number, z: Number,
    _id: false,
  }],
}, { timestamps: true })

//  dev-      hot-reload
if (process.env.NODE_ENV !== 'production') {
  delete mongoose.models.Attempt
}

export default mongoose.model('Attempt', AttemptSchema)
