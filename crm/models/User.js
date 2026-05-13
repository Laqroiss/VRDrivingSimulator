import mongoose from 'mongoose'
import bcrypt from 'bcryptjs'

const UserSchema = new mongoose.Schema({
  phone:    { type: String, required: true, unique: true, trim: true },
  fullName: { type: String, required: true, trim: true },
  password: { type: String, required: true },
}, { timestamps: true })

UserSchema.pre('save', async function (next) {
  if (!this.isModified('password')) return next()
  this.password = await bcrypt.hash(this.password, 10)
  next()
})

UserSchema.methods.comparePassword = function (plain) {
  return bcrypt.compare(plain, this.password)
}

if (process.env.NODE_ENV !== 'production') {
  delete mongoose.models.User
}

export default mongoose.model('User', UserSchema)
